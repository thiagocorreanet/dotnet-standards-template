using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Shared.Data.Transactions;
using Shared.Http.Results;
namespace Shared.WebHost.Middleware;
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        var database = exception is DbUpdateException { InnerException: PostgresException pg } ? pg : exception as PostgresException;
        var (status, code, title, detail) = exception switch
        {
            CommitOutcomeUnknownException => (503, "CommitOutcomeUnknown", "Resultado não confirmado", "Resultado não confirmado. Consulte o recurso antes de reenviar."),
            DbUpdateConcurrencyException => (409, "ConcurrencyConflict", "Conflito de concorrência", "O recurso foi alterado. Atualize os dados e tente novamente."),
            NpgsqlException { IsTransient: true } => (503, "DatabaseUnavailable", "Serviço indisponível", "Dependência temporariamente indisponível."),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => (499, "RequestCanceled", "Requisição cancelada", "Requisição cancelada."),
            _ when database?.SqlState is "23505" or "23P01" => (409, "ConstraintConflict", "Conflito", "A operação conflita com um recurso existente."),
            _ when database?.SqlState is "55P03" or "40001" or "40P01" => (503, "ResourceBusy", "Recurso ocupado", "Recurso ocupado. Tente novamente."),
            _ => (500, "UnexpectedError", "Erro interno", "Ocorreu um erro inesperado. Informe o traceId ao suporte.")
        };
        logger.LogError(exception, "Falha {ErrorCode}, TraceId={TraceId}", code, traceId);
        if (context.RequestAborted.IsCancellationRequested) return true;
        var problem = new ProblemDetails { Status = status, Title = title, Type = ResultHttpExtensions.ProblemTypeBase + code, Detail = detail };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = traceId;
        if (exception is CommitOutcomeUnknownException unknown) problem.Extensions["operationId"] = unknown.OperationId;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken: ct);
        return true;
    }
}
