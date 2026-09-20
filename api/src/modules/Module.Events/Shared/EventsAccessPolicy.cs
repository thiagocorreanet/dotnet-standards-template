using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Contracts.People;
using Shared.Http.Endpoints;
using Module.Events.UseCases.CreateEvent;
using Module.Events.UseCases.UpdateEvent;
using Module.Events.UseCases.DeleteEvent;
using Module.Events.UseCases.ChangeEventStatus;
using Module.Events.UseCases.ListEvents;
using Module.Events.UseCases.GetEvent;
using Module.Events.UseCases.ListTracks;
using Module.Events.UseCases.AddTrack;
using Module.Events.UseCases.UpdateTrack;
using Module.Events.UseCases.DeleteTrack;
using Module.Events.UseCases.RegisterParticipant;
using Module.Events.UseCases.CancelRegistration;
using Module.Events.UseCases.ListRegistrations;
namespace Module.Events.Shared;
internal sealed class EventsAccessPolicy(EventsDbContext db, ICurrentUser user, IPeopleModuleApi people) : IModuleAccessPolicy
{
    public Assembly ModuleAssembly => typeof(EventsModule).Assembly;
    public async Task<bool> CanExecuteAsync(object request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        if (request is ListEventsRequest or GetEventRequest or ListTracksRequest) return true;
        if (request is CreateEventRequest) return user.HasRole(DefaultRoles.Organizer);
        var id = request switch
        {
            UpdateEventRequest r => r.EventId, DeleteEventRequest r => r.EventId,
            ChangeEventStatusRequest r => r.EventId, AddTrackRequest r => r.EventId,
            UpdateTrackRequest r => r.EventId, DeleteTrackRequest r => r.EventId,
            RegisterParticipantRequest r => r.EventId, CancelRegistrationRequest r => r.EventId,
            ListRegistrationsRequest r => r.EventId, _ => Guid.Empty
        };
        if (id == Guid.Empty) return false;
        if (user.HasRole(DefaultRoles.Organizer) && await db.Events.AnyAsync(e => e.Id == id && e.OrganizerId == user.Id, ct))
            return true;
        if (request is RegisterParticipantRequest registration)
            return await people.BelongsToUserAsync(registration.PersonId, user.Id.Value, ct);
        if (request is CancelRegistrationRequest cancellation)
        {
            var person = await db.Registrations.Where(i => i.Id == cancellation.RegistrationId && i.EventId == cancellation.EventId)
                .Select(i => (Guid?)i.PersonId).SingleOrDefaultAsync(ct);
            return person.HasValue && await people.BelongsToUserAsync(person.Value, user.Id.Value, ct);
        }
        return false;
    }
}
