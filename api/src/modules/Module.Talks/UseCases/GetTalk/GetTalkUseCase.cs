using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Contracts.Events;
using Shared.Contracts.Venues;
using Shared.Contracts.People;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.GetTalk;

/// <summary>Projeção da palestra enriquecida com nomes de evento, sala e pessoas obtidos via contratos (uma chamada em lote para pessoas).</summary>
internal sealed class GetTalkUseCase(
    TalksDbContext db,
    IEventsModuleApi eventsApi,
    IVenuesModuleApi venuesApi,
    IPeopleModuleApi peopleApi,
    ILogger<GetTalkUseCase> logger) : IUseCase<GetTalkRequest, GetTalkResponse>
{
    public async Task<Result<GetTalkResponse>> HandleAsync(GetTalkRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.GetTalk")
            .AsNoTracking()
            .Where(p => p.Id == request.TalkId)
            .Select(p => new
            {
                p.Id,
                p.EventId,
                p.TrackId,
                p.RoomId,
                p.TalkTitle,
                p.TalkDescription,
                p.TalkStart,
                p.TalkEnd,
                Speakers = p.Speakers.OrderBy(x => x.CreatedAt).Select(x => new { x.PersonId, x.SpeakerRole }).ToList(),
                Contents = p.Contents.OrderBy(c => c.CreatedAt)
                    .Select(c => new GetTalkContentResponse(c.Id, c.ContentTitle, c.ContentType, c.ContentUrl, c.ContentDescription))
                    .ToList(),
                AttendancesCount = p.Attendances.Count,
                CertificatesCount = p.Certificates.Count,
                p.CreatedAt,
                p.UpdatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (talk is null)
        {
            logger.LogInformation("Palestra {TalkId} não encontrada para detalhamento", request.TalkId);
            return TalksErrors.TalkNotFound;
        }

        logger.LogDebug("Consultando módulo Eventos para enriquecer evento {EventId} e trilha {TrackId} da palestra {TalkId}", talk.EventId, talk.TrackId, talk.Id);
        var eventEntity = await eventsApi.GetEventSummaryAsync(talk.EventId, cancellationToken);
        var track = await eventsApi.GetTrackSummaryAsync(talk.EventId, talk.TrackId, cancellationToken);
        RoomSummary? room;
        if (talk.RoomId.HasValue)
        {
            logger.LogDebug("Consultando módulo Locais para enriquecer sala {RoomId} da palestra {TalkId}", talk.RoomId, talk.Id);
            room = await venuesApi.GetRoomSummaryAsync(talk.RoomId.Value, cancellationToken);
        }
        else
        {
            logger.LogDebug("Palestra {TalkId} não possui sala; chamada ao módulo Locais ignorada", talk.Id);
            room = null;
        }
        var personIds = talk.Speakers.Select(x => x.PersonId).Distinct().ToList();
        logger.LogDebug("Consultando módulo Pessoas para enriquecer {SpeakerCount} palestrante(s) da palestra {TalkId}", personIds.Count, talk.Id);
        var people = await peopleApi.GetPeopleSummaryAsync(personIds, cancellationToken);
        var names = people.ToDictionary(p => p.Id, p => p.PersonName);
        logger.LogInformation(
            "Palestra {TalkId} carregada com {SpeakerCount} palestrante(s), {ContentCount} conteúdo(s), {AttendanceCount} presença(s) e {CertificateCount} certificado(s); referências ausentes: evento={MissingEvent}, trilha={MissingTrack}, sala={MissingRoom}, pessoas={MissingPersonCount}",
            talk.Id, talk.Speakers.Count, talk.Contents.Count, talk.AttendancesCount, talk.CertificatesCount,
            eventEntity is null, track is null, talk.RoomId.HasValue && room is null, personIds.Count(id => !names.ContainsKey(id)));

        logger.LogDebug("Mapeando {SpeakerCount} palestrante(s) e {ContentCount} conteúdo(s) para a resposta", talk.Speakers.Count, talk.Contents.Count);
        return new GetTalkResponse(
            talk.Id,
            talk.EventId,
            eventEntity?.EventName ?? string.Empty,
            talk.TrackId,
            track?.TrackName ?? string.Empty,
            talk.RoomId,
            room?.RoomName,
            talk.TalkTitle,
            talk.TalkDescription,
            talk.TalkStart,
            talk.TalkEnd,
            (int)(talk.TalkEnd - talk.TalkStart).TotalMinutes,
            talk.Speakers.Select(x => new GetTalkSpeakerResponse(x.PersonId, names.GetValueOrDefault(x.PersonId, string.Empty), x.SpeakerRole)).ToList(),
            talk.Contents,
            talk.AttendancesCount,
            talk.CertificatesCount,
            talk.CreatedAt,
            talk.UpdatedAt);
    }
}
