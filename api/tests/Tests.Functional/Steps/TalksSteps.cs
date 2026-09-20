using System.Net;
using Reqnroll;
using Shouldly;

namespace Tests.Functional.Steps;

[Binding]
public sealed class TalksSteps(ScenarioContext context)
{
    private DateTimeOffset Start => DateTimeOffset.UtcNow.AddDays(10);

    [Given("que existem evento, sala e palestrante para a palestra")]
    public async Task PrepareDependencies()
    {
        var venue = await context.PostAsAdministratorAsync("/api/v1/venues", new { venueName = context.UniqueName("Local Palestra"), addressCity = "Vila Velha", addressState = "ES", singleRoomCapacity = 100 });
        venue.EnsureSuccessStatusCode();
        context.Ids["venue"] = (await context.JsonBodyAsync()).GetProperty("id").GetGuid();
        await context.GetAsync($"/api/v1/venues/{context.Ids["venue"]}");
        context.Ids["room"] = (await context.JsonBodyAsync()).GetProperty("rooms")[0].GetProperty("id").GetGuid();

        var person = await context.PostAsAdministratorAsync("/api/v1/people", new { personName = context.UniqueName("Speaker"), personEmail = $"speaker-{context.Suffix}@test.local" });
        person.EnsureSuccessStatusCode();
        context.Ids["person"] = (await context.JsonBodyAsync()).GetProperty("id").GetGuid();

        var eventEntity = await context.PostAsync("/api/v1/events", new { eventName = context.UniqueName("Evento Palestra"), eventStartDate = Start, eventEndDate = Start.AddHours(8), eventFormat = "InPerson", venueId = context.Ids["venue"] });
        eventEntity.EnsureSuccessStatusCode();
        context.Ids["eventEntity"] = (await context.JsonBodyAsync()).GetProperty("id").GetGuid();
        await context.GetAsync($"/api/v1/events/{context.Ids["eventEntity"]}");
        context.Ids["track"] = (await context.JsonBodyAsync()).GetProperty("tracks")[0].GetProperty("id").GetGuid();
    }

    [Given("que existe uma palestra cadastrada")]
    [When("crio uma palestra válida")]
    public async Task CreateTalk()
    {
        var response = await context.PostAsync("/api/v1/talks", Payload(new[] { new { personId = context.Ids["person"], speakerRole = "Principal" } }));
        if (response.StatusCode == HttpStatusCode.Created)
            context.Ids["talk"] = (await context.JsonBodyAsync()).GetProperty("id").GetGuid();
    }

    [When("tento criar uma palestra sem palestrantes")]
    public async Task CreateWithoutSpeakers() => await context.PostAsync("/api/v1/talks", Payload(Array.Empty<object>()));

    [When("adiciono o conteúdo {string} do tipo {string}")]
    public async Task AddContent(string title, string type) => await context.PostAsync($"/api/v1/talks/{context.Ids["talk"]}/contents", new
    {
        contentTitle = title, contentType = type, contentUrl = "https://content.test.local/slides.pdf",
    });

    [When("tento adicionar novamente o palestrante")]
    public async Task AddSpeakerDuplicate() => await context.PostAsync($"/api/v1/talks/{context.Ids["talk"]}/speakers", new
    {
        personId = context.Ids["person"], speakerRole = "Coauthor",
    });

    [Then("a palestra deve possuir {int} palestrante\\(s\\)")]
    public async Task ValidateSpeakers(int count)
    {
        await context.GetAsync($"/api/v1/talks/{context.Ids["talk"]}");
        (await context.JsonBodyAsync()).GetProperty("speakers").GetArrayLength().ShouldBe(count);
    }

    [Then("a palestra deve possuir o conteúdo {string}")]
    public async Task ValidateContent(string title)
    {
        await context.GetAsync($"/api/v1/talks/{context.Ids["talk"]}");
        (await context.JsonBodyAsync()).GetProperty("contents").EnumerateArray().Select(x => x.GetProperty("contentTitle").GetString()).ShouldContain(title);
    }

    private object Payload(object speakers) => new
    {
        eventId = context.Ids["eventEntity"], trackId = context.Ids["track"], roomId = context.Ids["room"], talkTitle = context.UniqueName("Monolito Modular"),
        talkStart = Start.AddHours(1), talkEnd = Start.AddHours(2), speakers,
    };
}
