using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Contracts.Events;
using Shared.Contracts.People;
using Shared.Http.Endpoints;
using Module.Talks.UseCases.CreateTalk;
using Module.Talks.UseCases.UpdateTalk;
using Module.Talks.UseCases.DeleteTalk;
using Module.Talks.UseCases.ListTalks;
using Module.Talks.UseCases.GetTalk;
using Module.Talks.UseCases.AddContent;
using Module.Talks.UseCases.RemoveContent;
using Module.Talks.UseCases.AddSpeaker;
using Module.Talks.UseCases.RemoveSpeaker;
using Module.Talks.UseCases.RecordAttendance;
using Module.Talks.UseCases.ListAttendances;
using Module.Talks.UseCases.IssueCertificate;
using Module.Talks.UseCases.ValidateCertificate;
namespace Module.Talks.Shared;
internal sealed class TalksAccessPolicy(TalksDbContext db, ICurrentUser user, IEventsModuleApi events, IPeopleModuleApi people) : IModuleAccessPolicy
{
    public Assembly ModuleAssembly => typeof(TalksModule).Assembly;
    public async Task<bool> CanExecuteAsync(object request, CancellationToken ct)
    {
        if (request is ValidateCertificateRequest) return true;
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        if (request is ListTalksRequest or GetTalkRequest) return true;
        if (request is CreateTalkRequest create)
            return user.HasRole(DefaultRoles.Organizer) && await events.BelongsToOrganizerAsync(create.EventId, user.Id.Value, ct);
        var id = request switch
        {
            UpdateTalkRequest r => r.TalkId, DeleteTalkRequest r => r.TalkId,
            AddContentRequest r => r.TalkId, RemoveContentRequest r => r.TalkId,
            AddSpeakerRequest r => r.TalkId, RemoveSpeakerRequest r => r.TalkId,
            RecordAttendanceRequest r => r.TalkId, ListAttendancesRequest r => r.TalkId,
            IssueCertificateRequest r => r.TalkId, _ => Guid.Empty
        };
        if (id == Guid.Empty) return false;
        var eventId = await db.Talks.Where(p => p.Id == id).Select(p => (Guid?)p.EventId).SingleOrDefaultAsync(ct);
        if (!eventId.HasValue) return false;
        if (user.HasRole(DefaultRoles.Organizer) && await events.BelongsToOrganizerAsync(eventId.Value, user.Id.Value, ct)) return true;
        return request is IssueCertificateRequest certificate && await people.BelongsToUserAsync(certificate.PersonId, user.Id.Value, ct);
    }
}
