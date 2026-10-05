using Shared.Contracts.Integration;

namespace Shared.Kernel.Entities;

/// <summary>Entidade que acumula eventos de integração para serem gravados no Outbox na mesma transação.</summary>
public interface IEventEmitter
{
    IReadOnlyCollection<IIntegrationEvent> Events { get; }
    void ClearEvents();
}
