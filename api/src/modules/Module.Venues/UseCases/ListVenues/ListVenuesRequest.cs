namespace Module.Venues.UseCases.ListVenues;

/// <summary>Filtros de listagem (query string). <c>Search</c> aplica sobre nome e cidade.</summary>
public sealed record ListVenuesRequest(string? Search, string? AddressState, bool? IsActive, int Page = 1, int PageSize = 20, string? SortBy = null, global::Shared.Contracts.Common.SortDirection Direction = global::Shared.Contracts.Common.SortDirection.Asc);
