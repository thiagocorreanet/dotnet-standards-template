using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;

namespace Tests.Integration.Events;

[Collection(ApiCollection.Name)]
public sealed class EventsTracksEndpointsTests(ApiFactory factory)
{
    private async Task<EventDetail> CreateAsync(HttpClient client)
    {
        var start = DateTimeOffset.UtcNow.AddDays(10);
        var response = await client.PostAsJsonAsync("/api/v1/events", new { eventName = $"Evento {Guid.NewGuid():N}", eventStartDate = start, eventEndDate = start.AddHours(8), eventFormat = "Remote", eventRemoteUrl = "https://eventEntity.test", tracks = new[] { new { trackName = "Arquitetura", trackColor = "#112233" } } });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await client.GetFromJsonAsync<EventDetail>(response.Headers.Location))!;
    }

    [Fact]
    public async Task Create_event_with_track_and_preserve_crud_with_soft_delete()
    {
        var client = factory.AuthenticatedClient(); var eventEntity = await CreateAsync(client);
        eventEntity.Tracks.ShouldHaveSingleItem().TrackName.ShouldBe("Arquitetura");
        var second = await client.PostAsJsonAsync($"/api/v1/events/{eventEntity.Id}/tracks", new { trackName = "Frontend", trackDescription = "Interfaces", trackColor = "#AABBCC" });
        second.StatusCode.ShouldBe(HttpStatusCode.Created); var created = (await second.Content.ReadFromJsonAsync<TrackDetail>())!;
        (await client.PutAsJsonAsync($"/api/v1/events/{eventEntity.Id}/tracks/{created.Id}", new { trackName = "Web", trackDescription = "React", trackColor = "#CCBBAA", isActive = false })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/v1/events/{eventEntity.Id}/tracks/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var last = eventEntity.Tracks.Single(); var lockHandle = await client.DeleteAsync($"/api/v1/events/{eventEntity.Id}/tracks/{last.Id}");
        lockHandle.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await TestProblemDetails.ReadAsync(lockHandle)).Code.ShouldBe("Events.EventRequiresTrack");
    }

    [Fact]
    public async Task Name_duplicate_and_color_invalid_should_return_problem_details()
    {
        var client = factory.AuthenticatedClient(); var eventEntity = await CreateAsync(client);
        var duplicate = await client.PostAsJsonAsync($"/api/v1/events/{eventEntity.Id}/tracks", new { trackName = "arquitetura", trackColor = "#FFFFFF" });
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var invalid = await client.PostAsJsonAsync($"/api/v1/events/{eventEntity.Id}/tracks", new { trackName = "Data", trackColor = "azul" });
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private sealed record EventDetail(Guid Id, List<TrackDetail> Tracks);
    private sealed record TrackDetail(Guid Id, string TrackName, bool IsActive);
}
