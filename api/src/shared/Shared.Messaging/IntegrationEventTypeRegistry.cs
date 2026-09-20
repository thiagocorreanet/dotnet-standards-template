using System.Collections.Frozen;
using Shared.Contracts.Integration;
namespace Shared.Messaging;
internal sealed class IntegrationEventTypeRegistry
{
    private readonly FrozenDictionary<string, Type> types = typeof(IIntegrationEvent).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IIntegrationEvent).IsAssignableFrom(t))
        .ToFrozenDictionary(t => EventContractAttribute.For(t).Name, t => t);
    public Type? Resolve(string name) => types.GetValueOrDefault(name);
}
