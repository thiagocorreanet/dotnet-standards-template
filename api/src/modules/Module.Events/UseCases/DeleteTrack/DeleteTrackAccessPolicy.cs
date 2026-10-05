using Module.Events.Shared;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.Events.UseCases.DeleteTrack;

/// <summary>Administrador ou o organizador dono do evento.</summary>
internal sealed class DeleteTrackAccessPolicy(EventOrganizerAccess organizer, ICurrentUser user) : IAccessPolicy<DeleteTrackRequest>
{
    public async Task<bool> CanExecuteAsync(DeleteTrackRequest request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        return request.EventId != Guid.Empty && await organizer.IsOrganizerOfAsync(request.EventId, ct);
    }
}
