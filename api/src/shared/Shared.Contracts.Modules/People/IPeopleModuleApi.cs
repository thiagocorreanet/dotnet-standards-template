using Shared.Contracts.Integration;

namespace Shared.Contracts.People;

/// <summary>Contrato síncrono do módulo Pessoas (palestrantes e participantes são Pessoas).</summary>
public interface IPeopleModuleApi
{
    Task<bool> BelongsToUserAsync(Guid personId, Guid userId, CancellationToken ct);
    Task<PersonSummary?> GetPersonSummaryAsync(Guid personId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PersonSummary>> GetPeopleSummaryAsync(IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken);
}

public sealed record PersonSummary(Guid Id, string PersonName, string PersonEmail);

[EventContract("people.person-created.v1", requiresConsumer: false)]
public sealed record PersonCreated(Guid PersonId) : IntegrationEvent;
[EventContract("people.person-deleted.v1", requiresConsumer: false)]
public sealed record PersonDeleted(Guid PersonId) : IntegrationEvent;
