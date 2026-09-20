using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Contracts.Talks;
using Shared.Contracts.People;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.CreateTalk;

/// <summary>Cria a palestra validando evento, período, sala (local e agenda) e palestrantes via contratos. Emite <see cref="TalkCreated"/>.</summary>
[Command("event-management-example")]
internal sealed class CreateTalkUseCase(
    TalksDbContext db,
    TalkScheduleChecker schedule,
    IPeopleModuleApi peopleApi,
    ILogger<CreateTalkUseCase> logger) : IUseCase<CreateTalkRequest, CreateTalkResponse>
{
    public async Task<Result<CreateTalkResponse>> HandleAsync(CreateTalkRequest request, CancellationToken cancellationToken)
    {
        logger.LogDebug("Validando agenda da nova palestra no evento {EventId}, trilha {TrackId} e sala {RoomId}", request.EventId, request.TrackId, request.RoomId);
        var verification = await schedule.CheckAsync(request.EventId, request.TrackId, request.RoomId, request.TalkStart, request.TalkEnd, cancellationToken);
        if (verification.IsFailure)
        {
            logger.LogInformation("Agenda da nova palestra rejeitada pela regra {ErrorCode}", verification.Error.Code);
            return verification.Error;
        }

        if (request.RoomId.HasValue)
        {
            logger.LogDebug("Verificando sobreposição da sala {RoomId} para nova palestra", request.RoomId);
            var occupiedRoom = await db.Talks
                .TagWith("Talks.CreateTalk.CheckRoom")
                .AnyAsync(TalkSchedule.OccupiesRoom(request.RoomId.Value, request.TalkStart, request.TalkEnd), cancellationToken);
            if (occupiedRoom)
            {
                logger.LogInformation("Nova palestra rejeitada porque a sala {RoomId} está ocupada no período solicitado", request.RoomId);
                return TalksErrors.RoomOccupied;
            }
            logger.LogDebug("Sala {RoomId} disponível no período solicitado", request.RoomId);
        }
        else
        {
            logger.LogDebug("Nova palestra não possui sala; verificação de sobreposição ignorada");
        }

        var personIds = request.Speakers.Select(p => p.PersonId).Distinct().ToList();
        logger.LogDebug("Consultando módulo Pessoas para validar {SpeakerCount} palestrante(s) distinto(s)", personIds.Count);
        var people = await peopleApi.GetPeopleSummaryAsync(personIds, cancellationToken);
        var missingSpeakers = personIds.Count(id => people.All(p => p.Id != id));
        if (missingSpeakers > 0)
        {
            logger.LogInformation("Nova palestra rejeitada: {MissingSpeakerCount} palestrante(s) não encontrado(s)", missingSpeakers);
            return TalksErrors.PersonNotFound;
        }
        logger.LogDebug("Todos os {SpeakerCount} palestrante(s) foram localizados no módulo Pessoas", personIds.Count);

        logger.LogDebug("Chamando fábrica de domínio Palestra.Criar com {SpeakerCount} palestrante(s)", request.Speakers.Count);
        var result = Talk.Create(
            request.EventId, request.TrackId, request.RoomId, request.TalkTitle, request.TalkDescription, request.TalkStart, request.TalkEnd,
            request.Speakers.Select(p => new NewSpeaker(p.PersonId, p.SpeakerRole)).ToList());
        if (result.IsFailure)
        {
            logger.LogInformation("Agregado rejeitou a criação da palestra pela regra {ErrorCode}", result.Error.Code);
            return result.Error;
        }

        var talk = result.Value;
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.Talks.Add(talk);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Palestra {TalkId} persistida no evento {EventId} com {SpeakerCount} palestrante(s)", talk.Id, talk.EventId, personIds.Count);
            return Result.Success(new CreateTalkResponse(talk.Id, talk.EventId, talk.TalkTitle));
        }, cancellationToken);
    }
}
