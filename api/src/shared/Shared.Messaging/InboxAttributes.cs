namespace Shared.Messaging;

/// <summary>
/// Nome estável do consumidor na Inbox. Sem o atributo, o nome é o <c>FullName</c> do handler: renomear a classe ou o
/// namespace cria um consumidor novo, que reaplica eventos ainda pendentes ou repetidos. Declare o nome antes de renomear.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class InboxConsumerAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>
/// Abre mão da Inbox para este handler. Use só quando o efeito já é idempotente no destino (ex.: chave do evento com
/// <c>ON CONFLICT DO NOTHING</c>) e registre a justificativa. O handler continua com escopo DI próprio.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SkipInboxAttribute(string justification) : Attribute
{
    public string Justification { get; } = justification;
}
