using System.Net;
using Reqnroll;
using Shouldly;

namespace Tests.Functional.Steps;

[Binding]
public sealed class EventsSteps(ScenarioContext context)
{
    [Given("que existe um evento remoto em rascunho")]
    [When("crio um evento remoto válido")]
    public async Task CreateEventRemote()
    {
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var response = await context.PostAsync("/api/v1/events", new
        {
            eventName = context.UniqueName("Evento Remoto"), eventStartDate = start,
            eventEndDate = start.AddHours(3), eventFormat = "Remote",
            eventRemoteUrl = "https://eventEntity.test.local/room",
        });
        if (response.StatusCode == HttpStatusCode.Created)
            context.Ids["eventEntity"] = (await context.JsonBodyAsync()).GetProperty("id").GetGuid();
    }

    [When("tento criar um evento presencial sem local")]
    public async Task CreateInPersonWithoutVenue()
    {
        var start = DateTimeOffset.UtcNow.AddDays(5);
        await context.PostAsync("/api/v1/events", new { eventName = context.UniqueName("InPerson"), eventStartDate = start, eventEndDate = start.AddHours(2), eventFormat = "InPerson" });
    }

    [When("tento criar um evento com período invertido")]
    public async Task CreatePeriodReversed()
    {
        var start = DateTimeOffset.UtcNow.AddDays(5);
        await context.PostAsync("/api/v1/events", new { eventName = context.UniqueName("Reversed"), eventStartDate = start, eventEndDate = start.AddHours(-1), eventFormat = "Remote", eventRemoteUrl = "https://eventEntity.test.local" });
    }

    [When("excluo o evento")]
    public async Task DeleteEvent() => await context.DeleteAsync($"/api/v1/events/{context.Ids["eventEntity"]}");

    [Then("o evento deve estar na situação {string}")]
    public async Task ValidateStatus(string status) => (await context.JsonBodyAsync()).GetProperty("eventStatus").GetString().ShouldBe(status);

    [Then("o evento não deve mais ser encontrado")]
    public async Task EventNotFound()
    {
        await context.GetAsync($"/api/v1/events/{context.Ids["eventEntity"]}");
        context.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [When("tento criar um evento remoto sem link")]
    public async Task CreateRemoteWithoutLink()
    {
        var start = DateTimeOffset.UtcNow.AddDays(5);
        await context.PostAsync("/api/v1/events", new { eventName = context.UniqueName("Sem link"), eventStartDate = start, eventEndDate = start.AddHours(2), eventFormat = "Remote" });
    }

    [When("tento criar um evento remoto com capacidade zero")]
    public async Task CreateCapacityZero()
    {
        var start = DateTimeOffset.UtcNow.AddDays(5);
        await context.PostAsync("/api/v1/events", new { eventName = context.UniqueName("Sem capacidade"), eventStartDate = start, eventEndDate = start.AddHours(2), eventFormat = "Remote", eventRemoteUrl = "https://eventEntity.test.local", eventMaximumCapacity = 0 });
    }

    [When("tento publicar o evento")]
    public async Task PublishEvent() => await context.PatchAsync($"/api/v1/events/{context.Ids["eventEntity"]}/status", new { eventStatus = "Published" });

    [When("crio um evento remoto com as trilhas {string} e {string}")]
    public async Task CreateWithTracks(string first, string second)
    {
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var response = await context.PostAsync("/api/v1/events", new { eventName = context.UniqueName("Evento com trilhas"), eventStartDate = start, eventEndDate = start.AddHours(4), eventFormat = "Remote", eventRemoteUrl = "https://eventEntity.test", tracks = new[] { new { trackName = first, trackColor = "#112233" }, new { trackName = second, trackColor = "#445566" } } });
        if (response.StatusCode == HttpStatusCode.Created) context.Ids["eventEntity"] = (await context.JsonBodyAsync()).GetProperty("id").GetGuid();
    }

    [Then("o evento deve apresentar {int} trilhas")]
    public async Task ValidateTrackCount(int count)
    {
        await context.GetAsync($"/api/v1/events/{context.Ids["eventEntity"]}");
        (await context.JsonBodyAsync()).GetProperty("tracks").GetArrayLength().ShouldBe(count);
    }

    [When("adiciono a trilha {string} ao evento")]
    public async Task AddTrack(string name) => await context.PostAsync($"/api/v1/events/{context.Ids["eventEntity"]}/tracks", new { trackName = name, trackColor = "#123456" });

    [Then("a trilha criada deve se chamar {string}")]
    public async Task ValidateTrackName(string name) => (await context.JsonBodyAsync()).GetProperty("trackName").GetString().ShouldBe(name);

    [When("tento excluir a única trilha do evento")]
    public async Task DeleteSingleTrack()
    {
        await context.GetAsync($"/api/v1/events/{context.Ids["eventEntity"]}");
        var trackId = (await context.JsonBodyAsync()).GetProperty("tracks")[0].GetProperty("id").GetGuid();
        await context.DeleteAsync($"/api/v1/events/{context.Ids["eventEntity"]}/tracks/{trackId}");
    }
}
