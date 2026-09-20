using Module.Events.Domain;

namespace Module.Events.UseCases.ListEvents;

/// <summary>Filtros de listagem (query string). <c>Search</c> aplica sobre nome e descrição; datas filtram <c>EventStartDate</c>.</summary>
public sealed record ListEventsRequest(
    string? Search,
    EventStatus? EventStatus,
    EventFormat? EventFormat,
    DateTimeOffset? StartDateFrom,
    DateTimeOffset? StartDateUntil,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    global::Shared.Contracts.Common.SortDirection Direction = global::Shared.Contracts.Common.SortDirection.Desc);
