using System.Collections;
using System.Reflection;

namespace Shared.Http.Endpoints;

/// <summary>
/// Extrai somente metadados seguros e úteis para diagnóstico do request. Textos livres, credenciais e demais dados
/// pessoais não entram no log. Coleções são representadas apenas pela quantidade de itens.
/// </summary>
internal static class UseCaseLogContext
{
    private static readonly string[] SafeSuffixes =
    [
        "Id", "Status", "Format", "IsActive", "Page", "Size", "Capacity", "Count"
    ];

    public static IReadOnlyDictionary<string, object?> From<TRequest>(TRequest request)
    {
        var context = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (request is null)
        {
            return context;
        }

        foreach (var property in typeof(TRequest).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            var value = property.GetValue(request);
            if (value is IEnumerable collection and not string)
            {
                var count = CountWithoutEnumeration(collection);
                if (count.HasValue)
                {
                    context[$"{property.Name}Count"] = count.Value;
                }
                continue;
            }

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (type.IsEnum || type == typeof(bool) || (type == typeof(Guid) || type == typeof(int)) && SafeSuffixes.Any(s => property.Name.EndsWith(s, StringComparison.Ordinal)))
            {
                context[property.Name] = value;
            }
        }

        return context;
    }

    private static int? CountWithoutEnumeration(IEnumerable collection)
    {
        if (collection is ICollection countedCollection)
        {
            return countedCollection.Count;
        }

        var countInterface = collection.GetType().GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>));
        return countInterface?.GetProperty(nameof(IReadOnlyCollection<object>.Count))?.GetValue(collection) as int?;
    }
}
