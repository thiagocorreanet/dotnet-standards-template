using Module.Talks.Shared;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.Talks.UseCases.AddSpeaker;

/// <summary>Administrador ou o organizador dono do evento da palestra.</summary>
internal sealed class AddSpeakerAccessPolicy(TalkOrganizerAccess organizer, ICurrentUser user) : IAccessPolicy<AddSpeakerRequest>
{
    public async Task<bool> CanExecuteAsync(AddSpeakerRequest request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        return await organizer.IsOrganizerOfTalkAsync(request.TalkId, ct);
    }
}
