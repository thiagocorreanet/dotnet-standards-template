using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Shared.Http.Endpoints;

/// <summary>
/// Chave de consistência de um comando, compilada na composição a partir de <see cref="CommandAttribute"/>.
/// Resolve o texto da chave para um request e o converte no identificador de <c>pg_advisory_xact_lock</c>.
/// </summary>
internal sealed class ConsistencyKey
{
    private readonly object[] _segments;

    private ConsistencyKey(string template, object[] segments)
    {
        Template = template;
        _segments = segments;
    }

    public string Template { get; }

    /// <summary>Compila o template do comando; placeholder inválido lança na composição (startup).</summary>
    /// <exception cref="InvalidOperationException">Template vazio, chaves desbalanceadas ou placeholder sem propriedade Guid/string.</exception>
    public static ConsistencyKey For(Type useCaseType, Type requestType, CommandAttribute command, string module)
    {
        var template = command.ConsistencyKey ?? module;
        if (string.IsNullOrWhiteSpace(template))
            throw Invalid(useCaseType, template, "a chave não pode ser vazia");

        var segments = new List<object>();
        var literal = new StringBuilder();
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] == '}') throw Invalid(useCaseType, template, "'}' sem '{' correspondente");
            if (template[i] != '{')
            {
                literal.Append(template[i]);
                continue;
            }
            var end = template.IndexOf('}', i + 1);
            var name = end < 0 ? "" : template[(i + 1)..end];
            if (end < 0 || name.Length == 0 || name.Contains('{', StringComparison.Ordinal))
                throw Invalid(useCaseType, template, "placeholder malformado");
            var property = requestType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property is not { CanRead: true } || (property.PropertyType != typeof(Guid) && property.PropertyType != typeof(string)))
                throw Invalid(useCaseType, template, $"{{{name}}} não corresponde a uma propriedade Guid ou string de {requestType.Name}");
            if (literal.Length > 0) segments.Add(literal.ToString());
            literal.Clear();
            segments.Add(property);
            i = end;
        }
        if (literal.Length > 0) segments.Add(literal.ToString());
        return new ConsistencyKey(template, [.. segments]);
    }

    /// <summary>Texto da chave para o request. Valores string são comparados sem diferenciar maiúsculas.</summary>
    /// <exception cref="InvalidOperationException">Placeholder com Guid vazio, string vazia ou request nulo.</exception>
    public string Resolve(object? request)
    {
        var key = new StringBuilder();
        foreach (var segment in _segments)
        {
            if (segment is string text)
            {
                key.Append(text);
                continue;
            }
            var property = (PropertyInfo)segment;
            var current = request is null ? null : property.GetValue(request);
            key.Append(current switch
            {
                Guid id when id != Guid.Empty => id.ToString("D"),
                string value when !string.IsNullOrWhiteSpace(value) => value.ToUpperInvariant(),
                _ => throw new InvalidOperationException(
                    $"Chave de consistência '{Template}' sem valor para {property.Name}: o recurso precisa estar identificado antes do lock."),
            });
        }
        return key.ToString();
    }

    /// <summary>Identificador do advisory lock: primeiros 8 bytes do SHA-256 da chave resolvida.</summary>
    public static long LockIdOf(string resolvedKey) =>
        BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(resolvedKey)));

    private static InvalidOperationException Invalid(Type useCaseType, string template, string reason) =>
        new($"[Command(\"{template}\")] em {useCaseType.Name}: {reason}.");
}
