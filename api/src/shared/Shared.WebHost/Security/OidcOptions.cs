namespace Shared.WebHost.Security;
public sealed class OidcOptions
{
    public const string Section = "Oidc";
    public const string UserIdClaim = "app_user_id";
    public const string RoleClaim = "app_role";
    public string Authority { get; set; } = "";
    public string Audience { get; set; } = "";
    public bool RequireHttpsMetadata { get; set; } = true;
    public int MaxAccessTokenLifetimeSeconds { get; set; } = 300;
    public string[] AllowedRoles { get; set; } = ["Administrator", "Organizer", "Participant"];
}
