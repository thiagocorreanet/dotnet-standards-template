using Module.Events.Shared;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.Events.UseCases.AddTrack;

/// <summary>Administrador ou o organizador dono do evento.</summary>
internal sealed class AddTrackAccessPolicy(EventOrganizerAccess organizer, ICurrentUser user) : IAccessPolicy<AddTrackRequest>
{
    public async Task<bool> CanExecuteAsync(AddTrackRequest request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        return request.EventId != Guid.Empty && await organizer.IsOrganizerOfAsync(request.EventId, ct);
    }
}
