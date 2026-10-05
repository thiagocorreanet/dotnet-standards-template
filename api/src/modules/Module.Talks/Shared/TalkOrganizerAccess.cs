using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Common;
using Shared.Contracts.Events;
using Shared.Contracts.Identity;

namespace Module.Talks.Shared;

/// <summary>Regra comum às policies do módulo: o usuário é organizador e dono do evento ao qual a palestra pertence.</summary>
internal sealed class TalkOrganizerAccess(TalksDbContext db, ICurrentUser user, IEventsModuleApi events)
{
    /// <summary>Evento da palestra, ou <c>null</c> quando a palestra não existe.</summary>
    public Task<Guid?> FindEventIdAsync(Guid talkId, CancellationToken ct) =>
        db.Talks.TagWith("Talks.Access.EventOfTalk").Where(t => t.Id == talkId).Select(t => (Guid?)t.EventId).SingleOrDefaultAsync(ct);

    public async Task<bool> IsOrganizerOfEventAsync(Guid eventId, CancellationToken ct) =>
        user.HasRole(DefaultRoles.Organizer) && user.Id is { } userId
        && await events.BelongsToOrganizerAsync(eventId, userId, ct);

    public async Task<bool> IsOrganizerOfTalkAsync(Guid talkId, CancellationToken ct)
    {
        if (talkId == Guid.Empty) return false;
        var eventId = await FindEventIdAsync(talkId, ct);
        return eventId.HasValue && await IsOrganizerOfEventAsync(eventId.Value, ct);
    }
}
