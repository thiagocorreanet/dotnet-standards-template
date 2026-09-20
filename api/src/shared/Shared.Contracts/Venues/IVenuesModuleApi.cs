using Shared.Contracts.Integration;

namespace Shared.Contracts.Venues;

/// <summary>Contrato síncrono do módulo Locais consumido por outros módulos (ex.: Eventos, Palestras).</summary>
public interface IVenuesModuleApi
{
    Task<VenueSummary?> GetVenueSummaryAsync(Guid venueId, CancellationToken cancellationToken);
    Task<RoomSummary?> GetRoomSummaryAsync(Guid roomId, CancellationToken cancellationToken);
}

public sealed record VenueSummary(Guid Id, string VenueName, int VenueTotalCapacity, int RoomsCount);
public sealed record RoomSummary(Guid Id, Guid VenueId, string RoomName, int RoomCapacity, string RoomType);

[EventContract("venues.venue-created.v1", requiresConsumer: false)]
public sealed record VenueCreated(Guid VenueId) : IntegrationEvent;
[EventContract("venues.venue-deleted.v1", requiresConsumer: false)]
public sealed record VenueDeleted(Guid VenueId) : IntegrationEvent;
