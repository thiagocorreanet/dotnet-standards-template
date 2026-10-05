using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Contracts.Common;
using Shared.Contracts.People;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Events.UseCases.ListRegistrations;

/// <summary>Lista inscrições do evento; nomes e e-mails vêm do módulo Pessoas em uma única chamada em lote.</summary>
internal sealed class ListRegistrationsUseCase(EventsDbContext db, IPeopleModuleApi people, ILogger<ListRegistrationsUseCase> logger) : IUseCase<ListRegistrationsRequest, PagedResult<ListRegistrationsItemResponse>>
{
    public async Task<Result<PagedResult<ListRegistrationsItemResponse>>> HandleAsync(ListRegistrationsRequest request, CancellationToken cancellationToken)
    {
        var eventExists = await db.Events
            .TagWith("Events.ListRegistrations.CheckEvent")
            .AnyAsync(e => e.Id == request.EventId, cancellationToken);
        if (!eventExists)
        {
            return EventsErrors.EventNotFound;
        }

        var query = db.Registrations
            .TagWith("Events.ListRegistrations")
            .AsNoTracking()
            .Where(i => i.EventId == request.EventId);

        if (request.RegistrationStatus.HasValue)
        {
            query = query.Where(i => i.RegistrationStatus == request.RegistrationStatus.Value);
        }

        var page = await query
            .OrderBy(i => i.RegistrationRegisteredAt)
            .Select(i => new RegistrationProjection(i.Id, i.PersonId, i.RegistrationStatus, i.RegistrationRegisteredAt))
            .ToPagedResultAsync(new PagedRequest(request.Page, request.PageSize), cancellationToken);

        var personIds = page.Items.Select(i => i.PersonId).Distinct().ToList();
        IReadOnlyList<PersonSummary> summaries;
        if (personIds.Count == 0)
        {
            summaries = [];
        }
        else
        {
            summaries = await people.GetPeopleSummaryAsync(personIds, cancellationToken);
        }
        var byId = summaries.ToDictionary(p => p.Id);
        var missingPeople = personIds.Count(id => !byId.ContainsKey(id));
        if (missingPeople > 0)
        {
            logger.LogWarning("Módulo Pessoas não retornou {MissingPersonCount} de {PersonCount} pessoa(s) referenciada(s) nas inscrições do evento {EventId}", missingPeople, personIds.Count, request.EventId);
        }

        var items = page.Items
            .Select(i =>
            {
                var person = byId.GetValueOrDefault(i.PersonId);
                return new ListRegistrationsItemResponse(i.Id, i.PersonId, person?.PersonName ?? string.Empty, person?.PersonEmail ?? string.Empty, i.RegistrationStatus, i.RegistrationRegisteredAt);
            })
            .ToList();

        return new PagedResult<ListRegistrationsItemResponse>(items, page.Page, page.PageSize, page.Total);
    }

    private sealed record RegistrationProjection(Guid Id, Guid PersonId, RegistrationStatus RegistrationStatus, DateTimeOffset RegistrationRegisteredAt);
}
