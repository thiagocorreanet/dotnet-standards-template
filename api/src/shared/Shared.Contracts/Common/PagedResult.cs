namespace Shared.Contracts.Common;

public enum SortDirection { Asc, Desc }

/// <summary>Resultado paginado padrão de todas as listagens da API.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

/// <summary>Parâmetros de paginação aceitos por todas as listagens (query string).</summary>
public sealed record PagedRequest(int Page = 1, int PageSize = 20)
{
    public const int MaximumPageSize = 100;
    public int NormalizedPage => Page < 1 ? 1 : Page;
    public int NormalizedSize => PageSize < 1 ? 20 : Math.Min(PageSize, MaximumPageSize);
}
