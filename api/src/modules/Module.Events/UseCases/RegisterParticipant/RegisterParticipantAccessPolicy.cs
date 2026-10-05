using Module.Events.Shared;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Contracts.People;
using Shared.Http.Endpoints;

namespace Module.Events.UseCases.RegisterParticipant;

/// <summary>Administrador, o organizador dono do evento ou o titular da pessoa inscrita.</summary>
internal sealed class RegisterParticipantAccessPolicy(EventOrganizerAccess organizer, IPeopleModuleApi people, ICurrentUser user) : IAccessPolicy<RegisterParticipantRequest>
{
    public async Task<bool> CanExecuteAsync(RegisterParticipantRequest request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        if (request.EventId == Guid.Empty) return false;
        if (await organizer.IsOrganizerOfAsync(request.EventId, ct)) return true;
        return await people.BelongsToUserAsync(request.PersonId, user.Id.Value, ct);
    }
}
