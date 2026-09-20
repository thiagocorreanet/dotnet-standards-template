using Shared.Contracts.Integration;

namespace Shared.Messaging;

/// <summary>Entrega um evento de integração aos manipuladores registrados. Hoje in-process; a interface permite trocar por broker.</summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}
