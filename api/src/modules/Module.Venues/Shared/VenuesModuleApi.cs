using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Venues;

namespace Module.Venues.Shared;

/// <summary>Implementação do contrato síncrono consumido por Eventos e Palestras. Projeções mínimas, sem tracking.</summary>
internal sealed class VenuesModuleApi(VenuesDbContext db) : IVenuesModuleApi
{
    public Task<VenueSummary?> GetVenueSummaryAsync(Guid venueId, CancellationToken cancellationToken) =>
        db.Venues
            .TagWith("Venues.ModuleApi.GetVenueSummary")
            .AsNoTracking()
            .Where(l => l.Id == venueId)
            .Select(l => new VenueSummary(l.Id, l.VenueName, l.Rooms.Sum(s => s.RoomCapacity), l.Rooms.Count))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<RoomSummary?> GetRoomSummaryAsync(Guid roomId, CancellationToken cancellationToken) =>
        db.Rooms
            .TagWith("Venues.ModuleApi.GetRoomSummary")
            .AsNoTracking()
            .Where(s => s.Id == roomId && s.IsActive)
            .Select(s => new RoomSummary(s.Id, s.VenueId, s.RoomName, s.RoomCapacity, s.RoomType.ToString()))
            .FirstOrDefaultAsync(cancellationToken);
}
