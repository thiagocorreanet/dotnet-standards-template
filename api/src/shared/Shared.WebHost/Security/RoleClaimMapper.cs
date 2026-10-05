using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Shared.Contracts.Identity;

namespace Shared.WebHost.Security;

/// <summary>
/// Lê os perfis externos do token conforme <see cref="OidcOptions.RoleClaimPath"/>, aplica <see cref="OidcOptions.RoleMap"/>
/// e devolve somente os perfis internos de <see cref="OidcOptions.AllowedRoles"/>.
/// </summary>
/// <remarks>Construído uma vez no registro: configuração inválida falha no startup, não na primeira requisição.</remarks>
internal sealed class RoleClaimMapper
{
    private const int MaxPathSegments = 6;
    private readonly string _claim;
    private readonly string[] _path;
    private readonly Dictionary<string, string> _map;
    private readonly HashSet<string> _allowed;

    public RoleClaimMapper(OidcOptions settings)
    {
        var segments = settings.RoleClaimPath.Split('.');
        if (segments.Length > MaxPathSegments || segments.Any(string.IsNullOrWhiteSpace) ||
            segments[0].Contains(OidcOptions.AudiencePlaceholder, StringComparison.Ordinal) ||
            segments[0] is OidcOptions.RoleClaim or OidcOptions.UserIdClaim)
            throw new InvalidOperationException(
                $"Oidc:RoleClaimPath inválido. Use até {MaxPathSegments} segmentos separados por ponto, começando pelo nome da claim (ex.: resource_access.{{audience}}.roles, realm_access.roles, roles).");
        if (settings.AllowedRoles.Any(role => !DefaultRoles.All.Contains(role, StringComparer.Ordinal)) ||
            settings.RoleMap.Any(entry => string.IsNullOrWhiteSpace(entry.Key) || !DefaultRoles.All.Contains(entry.Value, StringComparer.Ordinal)))
            throw new InvalidOperationException("Oidc:AllowedRoles e os valores de Oidc:RoleMap precisam ser perfis definidos em DefaultRoles.");

        // O marcador é trocado depois da divisão: uma audience com ponto continua sendo um único segmento.
        var resolved = segments.Select(s => s.Replace(OidcOptions.AudiencePlaceholder, settings.Audience, StringComparison.Ordinal)).ToArray();
        _claim = resolved[0];
        _path = resolved[1..];
        _map = new Dictionary<string, string>(settings.RoleMap, StringComparer.Ordinal);
        _allowed = new HashSet<string>(settings.AllowedRoles, StringComparer.Ordinal);
    }

    /// <summary>
    /// Devolve <c>false</c> quando a claim existe com formato inválido (JSON quebrado ou raiz que não é objeto): o token
    /// deve ser recusado. Claim ausente ou caminho inexistente resultam em nenhum perfil, não em falha.
    /// </summary>
    public bool TryMap(ClaimsPrincipal principal, out IReadOnlyList<string> roles)
    {
        roles = [];
        if (!TryReadExternal(principal, out var external)) return false;
        roles = external
            .Distinct(StringComparer.Ordinal)
            .Select(role => _map.TryGetValue(role, out var mapped) ? mapped : role)
            .Where(_allowed.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return true;
    }

    private bool TryReadExternal(ClaimsPrincipal principal, out IReadOnlyList<string> external)
    {
        external = [];
        try
        {
            if (_path.Length == 0)
            {
                // Arrays de primeiro nível chegam como várias claims com o mesmo nome; arrays aninhados chegam como JSON.
                external = principal.FindAll(_claim)
                    .SelectMany(c => c.ValueType == JsonClaimValueTypes.JsonArray ? Strings(c.Value) : [c.Value])
                    .ToArray();
                return true;
            }

            var raw = principal.FindFirstValue(_claim);
            if (raw is null) return true;
            using var json = JsonDocument.Parse(raw);
            if (json.RootElement.ValueKind != JsonValueKind.Object) return false;
            var node = json.RootElement;
            foreach (var segment in _path[..^1])
                if (!node.TryGetProperty(segment, out node) || node.ValueKind != JsonValueKind.Object) return true;
            if (node.TryGetProperty(_path[^1], out var leaf) && leaf.ValueKind == JsonValueKind.Array)
                external = StringsOf(leaf);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string[] Strings(string jsonArray)
    {
        using var json = JsonDocument.Parse(jsonArray);
        return json.RootElement.ValueKind == JsonValueKind.Array ? StringsOf(json.RootElement) : [];
    }

    private static string[] StringsOf(JsonElement array) =>
        array.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray();
}
