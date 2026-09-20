namespace Module.People.UseCases.ListPeople;

/// <summary>Filtros de listagem (query string). <c>Search</c> aplica sobre nome, e-mail e empresa.</summary>
public sealed record ListPeopleRequest(string? Search, bool? IsActive, int Page = 1, int PageSize = 20, string? SortBy = null, global::Shared.Contracts.Common.SortDirection Direction = global::Shared.Contracts.Common.SortDirection.Asc);
