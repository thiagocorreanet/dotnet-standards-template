using Microsoft.EntityFrameworkCore;
using Shared.Contracts.People;

namespace Module.People.Shared;

/// <summary>Implementação do contrato síncrono consumido por Eventos e Palestras. Projeções mínimas, sem tracking, apenas pessoas ativas.</summary>
internal sealed class PeopleModuleApi(PeopleDbContext db) : IPeopleModuleApi
{
    public Task<bool> BelongsToUserAsync(Guid personId, Guid userId, CancellationToken ct) =>
        db.People.AnyAsync(p => p.Id == personId && p.UserId == userId && p.IsActive, ct);
    public Task<PersonSummary?> GetPersonSummaryAsync(Guid personId, CancellationToken cancellationToken) =>
        db.People
            .TagWith("People.ModuleApi.GetPersonSummary")
            .AsNoTracking()
            .Where(p => p.Id == personId && p.IsActive && p.DeletedAt == null)
            .Select(p => new PersonSummary(p.Id, p.PersonName, p.PersonEmail))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PersonSummary>> GetPeopleSummaryAsync(IReadOnlyCollection<Guid> personIds, CancellationToken cancellationToken)
    {
        if (personIds.Count == 0)
        {
            return [];
        }

        var ids = personIds.Distinct().ToList();
        return await db.People
            .TagWith("People.ModuleApi.GetPeopleSummary")
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.IsActive && p.DeletedAt == null)
            .Select(p => new PersonSummary(p.Id, p.PersonName, p.PersonEmail))
            .ToListAsync(cancellationToken);
    }
}
