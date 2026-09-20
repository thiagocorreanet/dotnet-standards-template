using Shared.Contracts.Integration;

namespace Shared.Contracts.Talks;

/// <summary>Contrato síncrono do módulo Palestras (consumido por Eventos: um evento só é publicado com ao menos uma palestra).</summary>
public interface ITalksModuleApi
{
    Task<bool> IsRoomInUseAsync(Guid roomId, CancellationToken ct);
    Task<bool> IsTrackInUseAsync(Guid trackId, CancellationToken ct);
    Task<int> CountEventTalksAsync(Guid eventId, CancellationToken cancellationToken);
}

[EventContract("talks.talk-created.v1", requiresConsumer: false)]
public sealed record TalkCreated(Guid TalkId, Guid EventId) : IntegrationEvent;
[EventContract("talks.attendance-recorded.v1", requiresConsumer: false)]
public sealed record AttendanceRecorded(Guid TalkId, Guid EventId, Guid PersonId) : IntegrationEvent;
[EventContract("talks.certificate-issued.v1", requiresConsumer: false)]
public sealed record CertificateIssued(Guid CertificateId, Guid TalkId, Guid PersonId) : IntegrationEvent;
