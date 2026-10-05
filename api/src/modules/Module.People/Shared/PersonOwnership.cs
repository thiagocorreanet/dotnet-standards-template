using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Common;

namespace Module.People.Shared;

/// <summary>Regra comum às policies do módulo: a pessoa está vinculada ao usuário corrente.</summary>
internal sealed class PersonOwnership(PeopleDbContext db, ICurrentUser user)
{
    public async Task<bool> IsOwnerAsync(Guid personId, CancellationToken ct) =>
        personId != Guid.Empty
        && await db.People.TagWith("People.Access.Owner").AnyAsync(p => p.Id == personId && p.UserId == user.Id, ct);
}
