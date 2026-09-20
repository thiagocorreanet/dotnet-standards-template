using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Shared.Contracts.Common;
namespace Shared.WebHost.Security;
internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public Guid? Id => Guid.TryParse(Principal?.FindFirstValue(OidcOptions.UserIdClaim), out var id) ? id : null;
    public string? Name => Principal?.FindFirstValue("name");
    public string? Email => Principal?.FindFirstValue("email");
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
    public IReadOnlyCollection<string> Roles => Principal?.FindAll(OidcOptions.RoleClaim).Select(c => c.Value).ToArray() ?? [];
    public bool HasRole(string role) => Principal?.IsInRole(role) ?? false;
}
