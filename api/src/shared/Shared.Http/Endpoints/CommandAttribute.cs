namespace Shared.Http.Endpoints;

/// <summary>
/// Escrita transacional: o caso de uso roda numa transação que adquire um advisory lock antes de qualquer leitura.
/// Escritores que preservam a mesma invariante precisam declarar a mesma chave, inclusive rotinas de manutenção.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>[Command]</c>: a chave é o nome do módulo; todas as escritas do módulo fazem fila numa só.</item>
/// <item><c>[Command("events:{EventId}")]</c>: placeholders são lidos das propriedades <c>Guid</c> ou <c>string</c> do
/// request antes do lock; só escritas sobre o mesmo recurso fazem fila. Placeholder sem propriedade correspondente
/// falha no startup; valor vazio em runtime é contrato violado (valide <c>NotEmpty</c> no Validator).</item>
/// <item><c>[Command("chave-fixa")]</c>: conjunto explícito, compartilhado entre módulos quando a invariante atravessa
/// módulos (ver ADR-004).</item>
/// </list>
/// Uma chave por comando. Várias chaves exigiriam ordem estável de aquisição para evitar deadlock e não são suportadas.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CommandAttribute : Attribute
{
    /// <summary>Usa o nome do módulo como chave de consistência.</summary>
    public CommandAttribute()
    {
    }

    /// <param name="consistencyKey">Chave fixa ou template com placeholders <c>{Propriedade}</c> do request.</param>
    public CommandAttribute(string consistencyKey) => ConsistencyKey = consistencyKey;

    /// <summary>Chave ou template declarado; <c>null</c> significa o nome do módulo.</summary>
    public string? ConsistencyKey { get; }
}
