using Module.Talks.Shared;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.Talks.UseCases.UpdateTalk;

/// <summary>Administrador ou o organizador dono do evento da palestra.</summary>
internal sealed class UpdateTalkAccessPolicy(TalkOrganizerAccess organizer, ICurrentUser user) : IAccessPolicy<UpdateTalkRequest>
{
    public async Task<bool> CanExecuteAsync(UpdateTalkRequest request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        return await organizer.IsOrganizerOfTalkAsync(request.TalkId, ct);
    }
}
