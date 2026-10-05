using Microsoft.EntityFrameworkCore;
using Module.Audit.Shared;
using Module.Audit.UseCases.ListAuditRecords;
using SortDirection = Shared.Contracts.Common.SortDirection;
using Shouldly;

namespace Tests.Unit.Audit;

/// <summary>
/// Cada campo aceito pelo validator precisa virar ORDER BY na coluna certa. O SQL é gerado sem conexão (ToQueryString).
/// </summary>
public sealed class ListAuditRecordsSortTests : IDisposable
{
    private readonly AuditDbContext _db = new(new DbContextOptionsBuilder<AuditDbContext>()
        .UseNpgsql("Host=localhost;Database=not_conecta;Username=x;Password=x")
        .Options);

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData("module", "Module")]
    [InlineData("entityName", "EntityName")]
    [InlineData("ENTITYNAME", "EntityName")]
    [InlineData("operation", "Operation")]
    [InlineData("userName", "UserName")]
    [InlineData("occurredOn", "OccurredOn")]
    [InlineData(null, "OccurredOn")]
    public void Accepted_sort_field_orders_by_its_column_with_stable_tiebreak(string? sortBy, string column)
    {
        var ascending = ListAuditRecordsUseCase.Sort(_db.AuditRecords, sortBy, SortDirection.Asc).ToQueryString();
        var descending = ListAuditRecordsUseCase.Sort(_db.AuditRecords, sortBy, SortDirection.Desc).ToQueryString();

        ascending.ShouldContain($"ORDER BY a.\"{column}\", a.\"Id\"");
        descending.ShouldContain($"ORDER BY a.\"{column}\" DESC, a.\"Id\" DESC");
    }
}
