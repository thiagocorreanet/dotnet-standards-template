using Shared.Contracts.Integration;
namespace Shared.Contracts.Identity;
[EventContract("identity.user-registered.v1", requiresConsumer: false)]
public sealed record UserRegistered(Guid UserId) : IntegrationEvent;
