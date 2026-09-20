using Reqnroll;
using Shouldly;

namespace Tests.Functional.Steps;

[Binding]
public sealed class AuditSteps(ScenarioContext context)
{
    [When("consulto os registros de auditoria")]
    public async Task Query() => await context.GetAsync("/api/v1/audit/records?page=1&pageSize=10");

    [Then("a auditoria deve retornar uma coleção paginada")]
    public async Task ValidatePaging()
    {
        var json = await context.JsonBodyAsync();
        json.TryGetProperty("items", out var items).ShouldBeTrue();
        items.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Array);
        json.GetProperty("page").GetInt32().ShouldBe(1);
    }
}
