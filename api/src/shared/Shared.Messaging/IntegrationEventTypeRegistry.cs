using System.Collections.Frozen;
using System.Reflection;
using Shared.Contracts.Integration;
namespace Shared.Messaging;
/// <summary>
/// Resolve o nome estável (<see cref="EventContractAttribute"/>) para o tipo do evento. Procura no Shared.Contracts genérico e
/// nos assemblies <c>Shared.Contracts.*</c> ao lado do host, onde ficam os contratos dos módulos do projeto.
/// Nome repetido entre assemblies falha na construção.
/// </summary>
internal sealed class IntegrationEventTypeRegistry
{
    private readonly FrozenDictionary<string, Type> types = ContractAssemblies()
        .SelectMany(a => a.GetTypes())
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IIntegrationEvent).IsAssignableFrom(t))
        .ToFrozenDictionary(t => EventContractAttribute.For(t).Name, t => t);
    public Type? Resolve(string name) => types.GetValueOrDefault(name);

    private static IEnumerable<Assembly> ContractAssemblies() =>
        Directory.EnumerateFiles(AppContext.BaseDirectory, "Shared.Contracts.*.dll")
            .Select(path => Assembly.Load(AssemblyName.GetAssemblyName(path)))
            .Append(typeof(IIntegrationEvent).Assembly)
            .Distinct();
}
