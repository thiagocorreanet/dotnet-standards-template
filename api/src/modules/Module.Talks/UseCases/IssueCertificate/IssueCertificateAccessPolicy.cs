using Module.Talks.Shared;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Contracts.People;
using Shared.Http.Endpoints;

namespace Module.Talks.UseCases.IssueCertificate;

/// <summary>Administrador, o organizador dono do evento da palestra ou o titular da pessoa certificada.</summary>
internal sealed class IssueCertificateAccessPolicy(TalkOrganizerAccess organizer, IPeopleModuleApi people, ICurrentUser user) : IAccessPolicy<IssueCertificateRequest>
{
    public async Task<bool> CanExecuteAsync(IssueCertificateRequest request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        if (request.TalkId == Guid.Empty) return false;
        var eventId = await organizer.FindEventIdAsync(request.TalkId, ct);
        if (eventId is null) return false;
        if (await organizer.IsOrganizerOfEventAsync(eventId.Value, ct)) return true;
        return await people.BelongsToUserAsync(request.PersonId, user.Id.Value, ct);
    }
}
