using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Talks;

namespace Module.Talks.Shared;

/// <summary>Implementação do contrato síncrono consumido por Eventos (publicar exige ao menos uma palestra). Sem tracking.</summary>
internal sealed class TalksModuleApi(TalksDbContext db) : ITalksModuleApi
{
    public Task<bool> IsRoomInUseAsync(Guid roomId, CancellationToken ct) =>
        db.Talks.AnyAsync(p => p.RoomId == roomId && p.IsActive, ct);
    public Task<bool> IsTrackInUseAsync(Guid trackId, CancellationToken ct) =>
        db.Talks.AnyAsync(p => p.TrackId == trackId && p.IsActive, ct);
    public Task<int> CountEventTalksAsync(Guid eventId, CancellationToken cancellationToken) =>
        db.Talks
            .TagWith("Talks.ModuleApi.CountTalksOfEvent")
            .AsNoTracking()
            .CountAsync(p => p.EventId == eventId && p.IsActive, cancellationToken);
}
