using FluentValidation.TestHelper;
using Module.Audit.UseCases.ListAuditRecords;

namespace Tests.Unit.Audit;

public sealed class ListAuditRecordsValidatorTests
{
    private readonly ListAuditRecordsValidator _validator = new();

    private static ListAuditRecordsRequest Empty() => new(null, null, null, null, null, null, null);

    [Fact]
    public void Should_accept_request_without_filters() => _validator.TestValidate(Empty()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("Insert")]
    [InlineData("update")]
    [InlineData("DELETE")]
    public void Should_accept_operations_known_without_distinguish_case(string operation) =>
        _validator.TestValidate(Empty() with { Operation = operation }).ShouldNotHaveValidationErrorFor(r => r.Operation);

    [Fact]
    public void Should_reject_operation_unknown() =>
        _validator.TestValidate(Empty() with { Operation = "Read" }).ShouldHaveValidationErrorFor(r => r.Operation);

    [Fact]
    public void Should_reject_interval_reversed()
    {
        var from = new DateTimeOffset(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);
        _validator.TestValidate(Empty() with { OccurredFrom = from, OccurredUntil = from.AddDays(-1) }).ShouldHaveValidationErrorFor(r => r.OccurredUntil);
    }

    [Fact]
    public void Should_accept_interval_with_bounds_equal()
    {
        var from = new DateTimeOffset(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);
        _validator.TestValidate(Empty() with { OccurredFrom = from, OccurredUntil = from }).ShouldNotHaveValidationErrorFor(r => r.OccurredUntil);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Should_limit_size_of_page(int size) =>
        _validator.TestValidate(Empty() with { PageSize = size }).ShouldHaveValidationErrorFor(r => r.PageSize);

    [Fact]
    public void Should_limit_size_of_module() =>
        _validator.TestValidate(Empty() with { Module = new string('m', 51) }).ShouldHaveValidationErrorFor(r => r.Module);
}
