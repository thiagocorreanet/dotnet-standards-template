namespace Shared.Data.Transactions;

/// <summary>Prova de commit na transação do negócio/Outbox. Não contém payload do request.</summary>
public sealed class CommandReceipt
{
    public Guid Id { get; init; }
    public DateTimeOffset CommittedAt { get; init; }
}
