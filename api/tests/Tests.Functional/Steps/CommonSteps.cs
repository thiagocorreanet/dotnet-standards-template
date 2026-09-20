using System.Net;
using Reqnroll;
using Shouldly;
using Tests.Integration.Infra;

namespace Tests.Functional.Steps;

[Binding]
public sealed class CommonSteps(ScenarioContext context)
{
    [Given("que estou autenticado como {string}")]
    public async Task GivenIAmAuthenticatedAs(string role) => await context.AuthenticateAs(role);

    [Then("a resposta deve ter status {int}")]
    public async Task ThenTheResponseShouldHaveStatus(int status)
    {
        var body = await context.LastResponse!.Content.ReadAsStringAsync();
        ((int)context.LastResponse.StatusCode).ShouldBe(status, body);
    }

    [Then("a resposta deve ser um problema com código {string}")]
    public async Task ThenTheResponseShouldBeAProblemWithCode(string code)
    {
        context.LastResponse!.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await TestProblemDetails.ReadAsync(context.LastResponse);
        problem.Code.ShouldBe(code);
        problem.TraceId.ShouldNotBeNullOrWhiteSpace();
    }

    [Then("a resposta deve ser não autorizada")]
    public void ThenTheResponseShouldBeUnauthorized() => context.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
}
