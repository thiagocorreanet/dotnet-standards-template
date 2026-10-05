using Microsoft.EntityFrameworkCore;
using Module.Events.Shared;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Contracts.People;
using Shared.Http.Endpoints;

namespace Module.Events.UseCases.CancelRegistration;

/// <summary>Administrador, o organizador dono do evento ou o titular da pessoa inscrita.</summary>
internal sealed class CancelRegistrationAccessPolicy(EventsDbContext db, EventOrganizerAccess organizer, IPeopleModuleApi people, ICurrentUser user) : IAccessPolicy<CancelRegistrationRequest>
{
    public async Task<bool> CanExecuteAsync(CancelRegistrationRequest request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        if (request.EventId == Guid.Empty) return false;
        if (await organizer.IsOrganizerOfAsync(request.EventId, ct)) return true;
        var person = await db.Registrations.TagWith("Events.CancelRegistration.Access")
            .Where(i => i.Id == request.RegistrationId && i.EventId == request.EventId)
            .Select(i => (Guid?)i.PersonId).SingleOrDefaultAsync(ct);
        return person.HasValue && await people.BelongsToUserAsync(person.Value, user.Id.Value, ct);
    }
}
