using Reqnroll;
using Shouldly;
namespace Tests.Functional.Steps;
[Binding]
public sealed class IdentitySteps(ScenarioContext context)
{
    [When("provisiono uma identidade externa")]
    [Given("que provisionei uma identidade externa")]
    public async Task Provisionar() => await context.PostAsync("/api/v1/identity/users", new
    {
        subject = "external|" + context.Suffix, userName = "Test user", userEmail = $"test-{context.Suffix}@example.test"
    });
    [When("consulto minha identidade")]
    public async Task Me() => await context.GetAsync("/api/v1/identity/users/me");
    [Then("a resposta não deve conter access token")]
    public async Task WithoutToken() => (await context.JsonBodyAsync()).TryGetProperty("accessToken", out _).ShouldBeFalse();
}
