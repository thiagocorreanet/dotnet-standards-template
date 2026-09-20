using System.Net;
using Reqnroll;
using Shouldly;

namespace Tests.Functional.Steps;

[Binding]
public sealed class PeopleSteps(ScenarioContext context)
{
    private string Email => $"person-{context.Suffix}@test.local";

    [Given("que cadastrei uma pessoa chamada {string}")]
    [When("cadastro uma pessoa chamada {string}")]
    public async Task RegisterPerson(string name)
    {
        var response = await context.PostAsync("/api/v1/people", new
        {
            personName = context.UniqueName(name),
            personEmail = Email,
            personCompany = "TIES",
        });
        if (response.StatusCode == HttpStatusCode.Created)
            context.Ids["person"] = (await context.JsonBodyAsync()).GetProperty("id").GetGuid();
    }

    [When("tento cadastrar outra pessoa com o mesmo e-mail")]
    public async Task RegisterDuplicatePerson() => await context.PostAsync("/api/v1/people", new
    {
        personName = context.UniqueName("Pessoa duplicada"),
        personEmail = Email,
    });

    [When("excluo a pessoa cadastrada")]
    public async Task DeletePerson() => await context.DeleteAsync($"/api/v1/people/{context.Ids["person"]}");

    [Then("consigo consultar a pessoa cadastrada")]
    public async Task QueryPerson()
    {
        await context.GetAsync($"/api/v1/people/{context.Ids["person"]}");
        context.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await context.JsonBodyAsync()).GetProperty("personEmail").GetString().ShouldBe(Email);
    }

    [Then("a pessoa cadastrada não deve mais ser encontrada")]
    public async Task PersonNotFound()
    {
        await context.GetAsync($"/api/v1/people/{context.Ids["person"]}");
        context.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [When("tento cadastrar uma pessoa com e-mail inválido")]
    public async Task RegisterInvalidEmail() => await context.PostAsync("/api/v1/people", new
    {
        personName = context.UniqueName("E-mail inválido"), personEmail = "email-invalid",
    });

    [When("altero o nome da pessoa para {string}")]
    public async Task ChangeName(string name) => await context.PutAsync($"/api/v1/people/{context.Ids["person"]}", new
    {
        personName = name, personEmail = Email, isActive = true,
    });

    [Then("consigo consultar a pessoa com o nome {string}")]
    public async Task QueryName(string name)
    {
        await context.GetAsync($"/api/v1/people/{context.Ids["person"]}");
        (await context.JsonBodyAsync()).GetProperty("personName").GetString().ShouldBe(name);
    }
}
