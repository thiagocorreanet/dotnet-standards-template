using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;

namespace Module.Events.Shared;

/// <summary>Regra comum às policies do módulo: o usuário é organizador e dono do evento.</summary>
internal sealed class EventOrganizerAccess(EventsDbContext db, ICurrentUser user)
{
    public async Task<bool> IsOrganizerOfAsync(Guid eventId, CancellationToken ct) =>
        user.HasRole(DefaultRoles.Organizer)
        && await db.Events.TagWith("Events.Access.Organizer").AnyAsync(e => e.Id == eventId && e.OrganizerId == user.Id, ct);
}
