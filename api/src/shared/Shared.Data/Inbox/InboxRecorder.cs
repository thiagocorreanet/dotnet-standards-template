using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace Shared.Data.Inbox;

/// <summary>Grava a entrada da Inbox na transação corrente do contexto do módulo consumidor.</summary>
public static class InboxRecorder
{
    /// <summary>Tamanho máximo do nome estável do consumidor.</summary>
    public const int MaxConsumerLength = 200;

    /// <summary>
    /// Insere <c>(eventId, consumer)</c> com <c>ON CONFLICT DO NOTHING</c>. Devolve <c>false</c> quando o par já existe,
    /// ou seja, o efeito já foi confirmado antes. Uma entrega concorrente do mesmo evento espera a transação que inseriu
    /// primeiro terminar: se ela confirmar, esta recebe <c>false</c>; se desfizer, esta insere.
    /// </summary>
    /// <remarks>Chame dentro de uma transação aberta no mesmo <paramref name="db"/> em que o handler grava.</remarks>
    public static async Task<bool> TryRecordAsync(DbContext db, Guid eventId, string consumer, CancellationToken ct)
    {
        var entity = db.Model.FindEntityType(typeof(InboxMessage))
            ?? throw new InvalidOperationException($"{db.GetType().Name} não mapeia a Inbox; derive de ModuleDbContext.");
        // Nome de tabela vem do modelo EF (código), nunca de entrada externa; valores seguem como parâmetros.
        var table = $"\"{entity.GetSchema()}\".\"{entity.GetTableName()}\"";
        var sql = FormattableStringFactory.Create(
            "INSERT INTO " + table + " (\"EventId\", \"Consumer\", \"ProcessedAt\") VALUES ({0}, {1}, clock_timestamp()) ON CONFLICT DO NOTHING",
            eventId, consumer);
        var inserted = await db.Database.ExecuteSqlAsync(sql, ct);
        return inserted == 1;
    }
}
