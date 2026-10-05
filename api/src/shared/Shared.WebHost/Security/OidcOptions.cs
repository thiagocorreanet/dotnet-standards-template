using Shared.Contracts.Identity;

namespace Shared.WebHost.Security;

public sealed class OidcOptions
{
    public const string Section = "Oidc";
    public const string UserIdClaim = "app_user_id";
    public const string RoleClaim = "app_role";
    /// <summary>Marcador de <see cref="RoleClaimPath"/> substituído pela <see cref="Audience"/>.</summary>
    public const string AudiencePlaceholder = "{audience}";

    public string Authority { get; set; } = "";
    public string Audience { get; set; } = "";
    public bool RequireHttpsMetadata { get; set; } = true;
    public int MaxAccessTokenLifetimeSeconds { get; set; } = 300;

    /// <summary>
    /// Perfis internos aceitos. Quando configurado, substitui a lista padrão (<see cref="DefaultRoles.All"/>);
    /// cada valor precisa existir em <see cref="DefaultRoles"/>.
    /// </summary>
    /// <remarks>
    /// Começa vazio de propósito: o binder de configuração acrescenta itens a um array já preenchido, o que impediria
    /// a configuração de reduzir a lista. O padrão é aplicado depois do binding, em <see cref="Normalize"/>.
    /// </remarks>
    public string[] AllowedRoles { get; set; } = [];

    /// <summary>
    /// Caminho da claim de perfis no token, com segmentos separados por ponto. O primeiro segmento é o nome da claim;
    /// os demais percorrem o JSON dela. <c>{audience}</c> é substituído pela <see cref="Audience"/>.
    /// Padrão Keycloak (client roles): <c>resource_access.{audience}.roles</c>.
    /// </summary>
    public string RoleClaimPath { get; set; } = "resource_access." + AudiencePlaceholder + ".roles";

    /// <summary>
    /// Mapa opcional de perfil externo para perfil interno, comparação exata. Perfil sem entrada no mapa é usado como
    /// veio. O resultado ainda passa pela <see cref="AllowedRoles"/>.
    /// </summary>
    public Dictionary<string, string> RoleMap { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Exige a claim <c>typ</c> igual a <see cref="TokenType"/> no payload. O Keycloak emite <c>typ: Bearer</c> no access
    /// token e <c>typ: ID</c> no id token. Desligue apenas para IdP que não emite essa claim e cuja audience já separa
    /// access token de id token.
    /// </summary>
    public bool RequireTokenType { get; set; } = true;

    public string TokenType { get; set; } = "Bearer";

    /// <summary>Aplica os padrões que dependem do binding (lista de perfis) e devolve a própria instância.</summary>
    public OidcOptions Normalize()
    {
        AllowedRoles = AllowedRoles.Length == 0 ? [.. DefaultRoles.All] : [.. AllowedRoles.Distinct(StringComparer.Ordinal)];
        return this;
    }
}
