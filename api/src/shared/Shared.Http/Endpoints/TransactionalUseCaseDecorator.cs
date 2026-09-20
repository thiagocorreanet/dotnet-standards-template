using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.Data.Transactions;
using Shared.Http.Results;

namespace Shared.Http.Endpoints;

/// <summary>Retry da unidade completa com escopo novo e prova de commit, antes de qualquer leitura de negócio.</summary>
internal sealed class TransactionalUseCaseDecorator<TRequest, TResponse>(
    IServiceScopeFactory scopeFactory, Type useCaseType, Type contextType, CommandAttribute command,
    IOptions<CommandTransactionOptions> options,
    ILogger<TransactionalUseCaseDecorator<TRequest, TResponse>> logger) : IUseCase<TRequest, TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        var operationId = Guid.CreateVersion7();
        Activity.Current?.SetTag("operation.id", operationId.ToString());
        var cfg = options.Value;
        for (var attempt = 1; ; attempt++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
            var observer = scope.ServiceProvider.GetRequiredService<ICommandTransactionObserver>();
            IDbContextTransaction? transaction = null;
            Result<TResponse>? result = null;
            var committing = false;
            try
            {
                transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                var digest = SHA256.HashData(Encoding.UTF8.GetBytes(command.ConsistencyKey));
                var lockId = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(digest);
                var lockTimeout = $"{cfg.LockTimeoutSeconds}s";
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('lock_timeout', {lockTimeout}, true)", cancellationToken);
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", cancellationToken);
                var inner = (IUseCase<TRequest, TResponse>)AuthorizedExecution.Create(scope.ServiceProvider, useCaseType, typeof(IUseCase<TRequest, TResponse>));
                result = await inner.HandleAsync(request, cancellationToken);
                if (result.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return result;
                }
                db.Set<CommandReceipt>().Add(new CommandReceipt { Id = operationId, CommittedAt = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync(cancellationToken);
                await observer.BeforeCommitAsync(operationId, attempt, cancellationToken);
                committing = true;
                await transaction.CommitAsync(cancellationToken);
                await observer.AfterCommitAsync(operationId, attempt, cancellationToken);
                return result;
            }
            catch (Exception exception)
            {
                if (transaction is not null)
                {
                    try { await transaction.DisposeAsync(); }
                    catch (Exception) { /* A verificação abaixo usa conexão nova e a mesma trava. */ }
                    transaction = null;
                }
                if (committing && await VerifyCommittedAsync(operationId, exception))
                {
                    logger.LogWarning("Confirmação recuperada da operação {OperationId}", operationId);
                    return result!;
                }
                if (!IsTransient(exception) || attempt >= cfg.MaxAttempts || cancellationToken.IsCancellationRequested)
                    throw;
                logger.LogWarning("Reexecutando operação {OperationId}, tentativa {Attempt}, causa {ErrorType}",
                    operationId, attempt, exception.GetType().Name);
                await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt + Random.Shared.Next(10, 50)), cancellationToken);
            }
            finally
            {
                if (transaction is not null) await transaction.DisposeAsync();
            }
        }
    }

    private async Task<bool> VerifyCommittedAsync(Guid operationId, Exception commitException)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            for (var verification = 0; verification < 3; verification++)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var db = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
                    await using var proof = await db.Database.BeginTransactionAsync(timeout.Token);
                    var lockId = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(
                        SHA256.HashData(Encoding.UTF8.GetBytes(command.ConsistencyKey)));
                    // Aguarda a conclusão da transação anterior antes de interpretar ausência como rollback.
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", timeout.Token);
                    return await db.Set<CommandReceipt>().TagWith("Infrastructure.VerifyCommit")
                        .AsNoTracking().AnyAsync(r => r.Id == operationId, timeout.Token);
                }
                catch (Exception ex) when (IsTransient(ex) && verification < 2 && !timeout.IsCancellationRequested)
                {
                    await Task.Delay(100, timeout.Token);
                }
            }
        }
        catch (Exception)
        {
            throw new CommitOutcomeUnknownException(operationId, commitException);
        }
        throw new CommitOutcomeUnknownException(operationId, commitException);
    }

    private static bool IsTransient(Exception exception) => exception switch
    {
        NpgsqlException { IsTransient: true } => true,
        TimeoutException => true,
        DbUpdateException { InnerException: { } inner } => IsTransient(inner),
        _ => false,
    };
}
