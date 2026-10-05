using Module.Events.Shared;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.Events.UseCases.ListRegistrations;

/// <summary>Administrador ou o organizador dono do evento.</summary>
internal sealed class ListRegistrationsAccessPolicy(EventOrganizerAccess organizer, ICurrentUser user) : IAccessPolicy<ListRegistrationsRequest>
{
    public async Task<bool> CanExecuteAsync(ListRegistrationsRequest request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        return request.EventId != Guid.Empty && await organizer.IsOrganizerOfAsync(request.EventId, ct);
    }
}
