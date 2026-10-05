namespace Shared.Data.Inbox;

/// <summary>
/// Registro de que o consumidor <see cref="Consumer"/> já aplicou o evento <see cref="EventId"/>. Gravado na mesma
/// transação do efeito do handler, no schema do módulo consumidor.
/// </summary>
public sealed class InboxMessage
{
    public Guid EventId { get; init; }
    public string Consumer { get; init; } = "";
    public DateTimeOffset ProcessedAt { get; init; }
}
