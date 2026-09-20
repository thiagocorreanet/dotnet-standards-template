using System.Data.Common;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Shared.Data.Interceptors;

/// <summary>
/// Regra do DBA: toda consulta precisa de <c>TagWith("Module.UseCase")</c> (vira um comentário SQL, visível em pg_stat_activity).
/// O interceptor não bloqueia, mas registra warning e métrica para consultas sem tag, tornando a regra mensurável.
/// </summary>
public sealed class QueryTagInterceptor(ILogger<QueryTagInterceptor> logger) : DbCommandInterceptor
{
    public static readonly Meter Meter = new("ModularApi.Data");
    private static readonly Counter<long> UntaggedQueries = Meter.CreateCounter<long>("db.queries.untagged", description: "Consultas SELECT executadas without TagWith");

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Check(command, eventData);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Check(command, eventData);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        RecordFailure(command, eventData);
        base.CommandFailed(command, eventData);
    }

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        RecordFailure(command, eventData);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private void Check(DbCommand command, CommandEventData eventData)
    {
        var sql = command.CommandText;
        if (sql.StartsWith("-- ", StringComparison.Ordinal) || sql.Contains("__EFMigrationsHistory", StringComparison.Ordinal) || !sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var context = eventData.Context?.GetType().Name ?? "unknown";
        UntaggedQueries.Add(1, new KeyValuePair<string, object?>("db.context", context));
        logger.LogDebug("Consulta sem TagWith detectada no contexto {DbContext}", context);
    }

    private void RecordFailure(DbCommand command, CommandErrorEventData eventData)
    {
        logger.LogError(
            "Banco falhou ao executar {DatabaseOperation} no contexto {DbContext} após {DurationMs:0.0} ms ({ErrorType})",
            GetOperation(command), eventData.Context?.GetType().Name ?? "unknown", eventData.Duration.TotalMilliseconds, eventData.Exception.GetType().Name);
    }

    private static string GetOperation(DbCommand command)
    {
        var firstLine = command.CommandText.AsSpan().TrimStart();
        if (firstLine.StartsWith("-- ", StringComparison.Ordinal))
        {
            firstLine = firstLine[3..];
            var end = firstLine.IndexOfAny('\r', '\n');
            return (end >= 0 ? firstLine[..end] : firstLine).Trim().ToString();
        }

        return command.CommandText.AsSpan().TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            ? "QueryWithoutTag"
            : "PersistenciaEF";
    }
}
