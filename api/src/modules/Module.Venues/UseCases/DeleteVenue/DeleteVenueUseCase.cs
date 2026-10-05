using Shared.Contracts.Events;
using Shared.Contracts.Talks;
using Microsoft.EntityFrameworkCore;
using Module.Venues.Domain;
using Module.Venues.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Venues.UseCases.DeleteVenue;

/// <summary>Exclusão lógica do local e de suas salas (interceptor converte Remove em soft delete).</summary>
[Command("event-management-example")]
internal sealed class DeleteVenueUseCase(VenuesDbContext db, IEventsModuleApi events, ITalksModuleApi talks) : IUseCase<DeleteVenueRequest, DeleteVenueResponse>
{
    public async Task<Result<DeleteVenueResponse>> HandleAsync(DeleteVenueRequest request, CancellationToken cancellationToken)
    {
        if (await events.IsVenueInUseAsync(request.VenueId, cancellationToken))
            return Error.Conflict("Venues.ResourceInUse", "Altere as referências antes de modificar ou excluir este recurso.");
        var venue = await db.Venues
            .TagWith("Venues.DeleteVenue.Load")
            .Include(l => l.Rooms)
            .FirstOrDefaultAsync(l => l.Id == request.VenueId, cancellationToken);
        if (venue is null)
        {
            return VenuesErrors.VenueNotFound;
        }

        // Excluir um evento não desativa suas palestras: uma palestra ativa pode continuar vinculada a uma sala
        // mesmo quando nenhum evento ativo referencia o local. Por isso cada sala é verificada, não só o local.
        foreach (var room in venue.Rooms)
        {
            if (await talks.IsRoomInUseAsync(room.Id, cancellationToken))
            {
                return Error.Conflict("Venues.ResourceInUse", "Altere as referências antes de modificar ou excluir este recurso.");
            }
        }

        venue.MarkDeleted();
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.Rooms.RemoveRange(venue.Rooms);
            db.Venues.Remove(venue);
            await db.SaveChangesAsync(ct);
            return Result.Success(new DeleteVenueResponse(venue.Id));
        }, cancellationToken);
    }
}
