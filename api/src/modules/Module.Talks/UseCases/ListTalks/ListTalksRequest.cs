namespace Module.Talks.UseCases.ListTalks;

/// <summary>Filtros de listagem (query string). <c>Search</c> aplica sobre o título.</summary>
public sealed record ListTalksRequest(Guid? EventId, string? Search, int Page = 1, int PageSize = 20, string? SortBy = null, global::Shared.Contracts.Common.SortDirection Direction = global::Shared.Contracts.Common.SortDirection.Asc);
