namespace Shared.Data.Transactions;

public sealed class CommandTransactionOptions
{
    public int MaxAttempts { get; set; } = 3;
    public int LockTimeoutSeconds { get; set; } = 10;
    public int CommandTimeoutSeconds { get; set; } = 30;
}

/// <summary>Hooks para diagnóstico e injeção de falhas em testes.</summary>
public interface ICommandTransactionObserver
{
    Task BeforeCommitAsync(Guid operationId, int attempt, CancellationToken cancellationToken);
    Task AfterCommitAsync(Guid operationId, int attempt, CancellationToken cancellationToken);
}

public sealed class NullCommandTransactionObserver : ICommandTransactionObserver
{
    public Task BeforeCommitAsync(Guid operationId, int attempt, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task AfterCommitAsync(Guid operationId, int attempt, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class CommitOutcomeUnknownException(Guid operationId, Exception inner)
    : Exception("Não foi possível confirmar a operação. Consulte o recurso antes de reenviar.", inner)
{
    public Guid OperationId { get; } = operationId;
}
