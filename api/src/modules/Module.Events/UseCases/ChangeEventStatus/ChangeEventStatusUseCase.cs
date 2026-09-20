using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Contracts.Talks;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Events.UseCases.ChangeEventStatus;

/// <summary>Aplica a máquina de estados do agregado. Publicar consulta o módulo Palestras (ao menos uma palestra).</summary>
[Command("event-management-example")]
internal sealed class ChangeEventStatusUseCase(EventsDbContext db, ITalksModuleApi talks, ILogger<ChangeEventStatusUseCase> logger) : IUseCase<ChangeEventStatusRequest, ChangeEventStatusResponse>
{
    public async Task<Result<ChangeEventStatusResponse>> HandleAsync(ChangeEventStatusRequest request, CancellationToken cancellationToken)
    {
        var eventEntity = await db.Events
            .TagWith("Events.ChangeEventStatus.Load")
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);
        if (eventEntity is null)
        {
            logger.LogInformation("Evento {EventId} não encontrado para transição para {TargetStatus}", request.EventId, request.EventStatus);
            return EventsErrors.EventNotFound;
        }

        logger.LogInformation("Avaliando transição do evento {EventId} de {CurrentStatus} para {TargetStatus}", eventEntity.Id, eventEntity.EventStatus, request.EventStatus);

        Result result;
        switch (request.EventStatus)
        {
            case EventStatus.Published:
                logger.LogDebug("Chamando fluxo de domínio para publicar evento {EventId}", eventEntity.Id);
                result = await PublishAsync(eventEntity, cancellationToken);
                break;
            case EventStatus.InProgress:
                logger.LogDebug("Chamando agregado Evento {EventId}.Iniciar", eventEntity.Id);
                result = eventEntity.Start();
                break;
            case EventStatus.Closed:
                logger.LogDebug("Chamando agregado Evento {EventId}.Encerrar", eventEntity.Id);
                result = eventEntity.Close();
                break;
            case EventStatus.Canceled:
                logger.LogDebug("Chamando agregado Evento {EventId}.Cancelar; motivo informado={HasReason}", eventEntity.Id, !string.IsNullOrWhiteSpace(request.Reason));
                result = eventEntity.Cancel(request.Reason);
                break;
            default:
                logger.LogInformation("Transição solicitada para situação não suportada no evento {EventId}", eventEntity.Id);
                result = Result.Failure(EventsErrors.InvalidStatusTransition);
                break;
        }
        if (result.IsFailure)
        {
            logger.LogInformation("Transição do evento {EventId} rejeitada pela regra {ErrorCode}", eventEntity.Id, result.Error.Code);
            return result.Error;
        }

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Evento {EventId} persistido na situação {EventStatus}", eventEntity.Id, eventEntity.EventStatus);
            return Result.Success(new ChangeEventStatusResponse(eventEntity.Id, eventEntity.EventStatus));
        }, cancellationToken);
    }

    private async Task<Result> PublishAsync(Event eventEntity, CancellationToken cancellationToken)
    {
        if (eventEntity.EventStatus != EventStatus.Draft)
        {
            logger.LogInformation("Publicação do evento {EventId} rejeitada porque a situação atual é {CurrentStatus}", eventEntity.Id, eventEntity.EventStatus);
            return EventsErrors.InvalidStatusTransition;
        }

        logger.LogDebug("Consultando módulo Palestras para validar a publicação do evento {EventId}", eventEntity.Id);
        var talkCount = await talks.CountEventTalksAsync(eventEntity.Id, cancellationToken);
        logger.LogInformation("Evento {EventId} possui {TalkCount} palestra(s) para avaliação da publicação", eventEntity.Id, talkCount);
        logger.LogDebug("Chamando agregado Evento {EventId}.Publicar com {TalkCount} palestra(s)", eventEntity.Id, talkCount);
        return eventEntity.Publish(talkCount);
    }
}
