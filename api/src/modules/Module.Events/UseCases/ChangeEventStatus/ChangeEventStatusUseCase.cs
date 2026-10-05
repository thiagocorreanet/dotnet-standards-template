using Microsoft.EntityFrameworkCore;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Contracts.Talks;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Events.UseCases.ChangeEventStatus;

/// <summary>Aplica a máquina de estados do agregado. Publicar consulta o módulo Palestras (ao menos uma palestra).</summary>
[Command("event-management-example")]
internal sealed class ChangeEventStatusUseCase(EventsDbContext db, ITalksModuleApi talks) : IUseCase<ChangeEventStatusRequest, ChangeEventStatusResponse>
{
    public async Task<Result<ChangeEventStatusResponse>> HandleAsync(ChangeEventStatusRequest request, CancellationToken cancellationToken)
    {
        var eventEntity = await db.Events
            .TagWith("Events.ChangeEventStatus.Load")
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);
        if (eventEntity is null)
        {
            return EventsErrors.EventNotFound;
        }

        Result result;
        switch (request.EventStatus)
        {
            case EventStatus.Published:
                result = await PublishAsync(eventEntity, cancellationToken);
                break;
            case EventStatus.InProgress:
                result = eventEntity.Start();
                break;
            case EventStatus.Closed:
                result = eventEntity.Close();
                break;
            case EventStatus.Canceled:
                result = eventEntity.Cancel(request.Reason);
                break;
            default:
                result = Result.Failure(EventsErrors.InvalidStatusTransition);
                break;
        }
        if (result.IsFailure)
        {
            return result.Error;
        }

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            return Result.Success(new ChangeEventStatusResponse(eventEntity.Id, eventEntity.EventStatus));
        }, cancellationToken);
    }

    private async Task<Result> PublishAsync(Event eventEntity, CancellationToken cancellationToken)
    {
        if (eventEntity.EventStatus != EventStatus.Draft)
        {
            return EventsErrors.InvalidStatusTransition;
        }

        var talkCount = await talks.CountEventTalksAsync(eventEntity.Id, cancellationToken);
        return eventEntity.Publish(talkCount);
    }
}
