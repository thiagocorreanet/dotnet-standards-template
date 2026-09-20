using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Contracts.People;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.AddSpeaker;

[Command("event-management-example")]
internal sealed class AddSpeakerUseCase(TalksDbContext db, IPeopleModuleApi peopleApi, ILogger<AddSpeakerUseCase> logger) : IUseCase<AddSpeakerRequest, AddSpeakerResponse>
{
    public async Task<Result<AddSpeakerResponse>> HandleAsync(AddSpeakerRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.AddSpeaker.Load")
            .Include(p => p.Speakers)
            .FirstOrDefaultAsync(p => p.Id == request.TalkId, cancellationToken);
        if (talk is null)
        {
            logger.LogInformation("Adição de palestrante rejeitada: palestra {TalkId} não encontrada", request.TalkId);
            return TalksErrors.TalkNotFound;
        }

        logger.LogDebug("Consultando módulo Pessoas para validar palestrante {PersonId}", request.PersonId);
        var person = await peopleApi.GetPersonSummaryAsync(request.PersonId, cancellationToken);
        if (person is null)
        {
            logger.LogInformation("Adição de palestrante à palestra {TalkId} rejeitada: pessoa {PersonId} não encontrada", talk.Id, request.PersonId);
            return TalksErrors.PersonNotFound;
        }

        logger.LogDebug("Chamando agregado Palestra {TalkId} para adicionar pessoa {PersonId} no papel {SpeakerRole}", talk.Id, request.PersonId, request.SpeakerRole);
        var result = talk.AddSpeaker(request.PersonId, request.SpeakerRole);
        if (result.IsFailure)
        {
            logger.LogInformation("Agregado Palestra {TalkId} rejeitou palestrante {PersonId} pela regra {ErrorCode}", talk.Id, request.PersonId, result.Error.Code);
            return result.Error;
        }

        var speaker = result.Value;
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Pessoa {PersonId} adicionada à palestra {TalkId}; total de palestrantes={SpeakerCount}", speaker.PersonId, talk.Id, talk.Speakers.Count);
            return Result.Success(new AddSpeakerResponse(speaker.TalkId, speaker.PersonId, speaker.SpeakerRole));
        }, cancellationToken);
    }
}
