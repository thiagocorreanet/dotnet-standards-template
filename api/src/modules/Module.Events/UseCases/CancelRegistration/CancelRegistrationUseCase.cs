using Microsoft.EntityFrameworkCore;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Events.UseCases.CancelRegistration;

/// <summary>Cancelamento não é soft delete: a inscrição permanece com situação <c>Canceled</c> e data de cancelamento.</summary>
[Command("event-management-example")]
internal sealed class CancelRegistrationUseCase(EventsDbContext db, TimeProvider timeProvider) : IUseCase<CancelRegistrationRequest, CancelRegistrationResponse>
{
    public async Task<Result<CancelRegistrationResponse>> HandleAsync(CancelRegistrationRequest request, CancellationToken cancellationToken)
    {
        var eventEntity = await db.Events
            .TagWith("Events.CancelRegistration.Load")
            .Include(e => e.Registrations.Where(i => i.Id == request.RegistrationId))
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);
        if (eventEntity is null)
        {
            return EventsErrors.EventNotFound;
        }

        var result = eventEntity.CancelRegistration(request.RegistrationId, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return result.Error;
        }

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            return Result.Success(new CancelRegistrationResponse(result.Value.Id));
        }, cancellationToken);
    }
}
