using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Identity;
namespace Module.Identity.Shared;
internal sealed class IdentityResolver(IdentityDbContext db) : IIdentityResolver
{
    public Task<IdentityAccount?> ResolveAsync(string issuer, string subject, CancellationToken ct) =>
        db.Users.TagWith("Identity.ResolveExternalIdentity").AsNoTracking()
          .Where(x => x.Issuer == issuer && x.Subject == subject)
          .Select(x => new IdentityAccount(x.Id, x.IsActive, x.TokensValidAfter)).SingleOrDefaultAsync(ct);
}
