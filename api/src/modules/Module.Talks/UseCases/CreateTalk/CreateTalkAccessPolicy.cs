using Module.Talks.Shared;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.Talks.UseCases.CreateTalk;

/// <summary>Administrador ou o organizador dono do evento da palestra.</summary>
internal sealed class CreateTalkAccessPolicy(TalkOrganizerAccess organizer, ICurrentUser user) : IAccessPolicy<CreateTalkRequest>
{
    public async Task<bool> CanExecuteAsync(CreateTalkRequest request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        return await organizer.IsOrganizerOfEventAsync(request.EventId, ct);
    }
}
