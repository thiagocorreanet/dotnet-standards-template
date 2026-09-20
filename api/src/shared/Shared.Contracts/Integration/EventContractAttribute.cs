using System.Reflection;
namespace Shared.Contracts.Integration;
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EventContractAttribute(string name, bool requiresConsumer = true) : Attribute
{
    public string Name { get; } = name;
    public bool RequiresConsumer { get; } = requiresConsumer;
    public static EventContractAttribute For(Type type) => type.GetCustomAttribute<EventContractAttribute>()
        ?? throw new InvalidOperationException($"Contrato estável ausente em {type.Name}.");
}
