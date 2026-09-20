namespace Shared.Http.Endpoints;

/// <summary>Escrita transacional. Módulos que preservam a mesma invariante declaram a mesma chave.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CommandAttribute(string consistencyKey) : Attribute
{
    public string ConsistencyKey { get; } = consistencyKey;
}
