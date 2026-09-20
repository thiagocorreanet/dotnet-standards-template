using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Events.Domain;
using Module.Events.Shared;
using Shared.Contracts.Common;
using Shared.Contracts.People;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

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
            logger.LogInformation("Listagem de inscrições rejeitada: evento {EventId} não encontrado", request.EventId);
            return EventsErrors.EventNotFound;
        }

        var query = db.Registrations
            .TagWith("Events.ListRegistrations")
            .AsNoTracking()
            .Where(i => i.EventId == request.EventId);

        if (request.RegistrationStatus.HasValue)
        {
            logger.LogDebug("Aplicando filtro de situação {RegistrationStatus} às inscrições do evento {EventId}", request.RegistrationStatus, request.EventId);
            query = query.Where(i => i.RegistrationStatus == request.RegistrationStatus.Value);
        }
        else
        {
            logger.LogDebug("Listagem de inscrições do evento {EventId} inclui todas as situações", request.EventId);
        }

        var page = await query
            .OrderBy(i => i.RegistrationRegisteredAt)
            .Select(i => new RegistrationProjection(i.Id, i.PersonId, i.RegistrationStatus, i.RegistrationRegisteredAt))
            .ToPagedResultAsync(new PagedRequest(request.Page, request.PageSize), cancellationToken);

        var personIds = page.Items.Select(i => i.PersonId).Distinct().ToList();
        logger.LogInformation("Página de inscrições do evento {EventId} contém {RegistrationCount} item(ns) e {PersonCount} pessoa(s) distinta(s)", request.EventId, page.Items.Count, personIds.Count);
        logger.LogDebug("Consultando módulo Pessoas em lote para enriquecer {PersonCount} inscrição(ões)", personIds.Count);
        IReadOnlyList<PersonSummary> summaries;
        if (personIds.Count == 0)
        {
            logger.LogDebug("Página sem inscrições; chamada ao módulo Pessoas ignorada");
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
        else
        {
            logger.LogDebug("Todas as {PersonCount} pessoa(s) das inscrições foram localizadas", personIds.Count);
        }

        logger.LogDebug("Mapeando {RegistrationCount} inscrição(ões) com os resumos de pessoas", page.Items.Count);
        var items = page.Items
            .Select(i =>
            {
                var person = byId.GetValueOrDefault(i.PersonId);
                return new ListRegistrationsItemResponse(i.Id, i.PersonId, person?.PersonName ?? string.Empty, person?.PersonEmail ?? string.Empty, i.RegistrationStatus, i.RegistrationRegisteredAt);
            })
            .ToList();

        logger.LogInformation("Listagem do evento {EventId} produziu {RegistrationCount} inscrição(ões) enriquecida(s)", request.EventId, items.Count);

        return new PagedResult<ListRegistrationsItemResponse>(items, page.Page, page.PageSize, page.Total);
    }

    private sealed record RegistrationProjection(Guid Id, Guid PersonId, RegistrationStatus RegistrationStatus, DateTimeOffset RegistrationRegisteredAt);
}
