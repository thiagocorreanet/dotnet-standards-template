using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;

namespace Tests.Integration.Modules;

[Collection(ApiCollection.Name)]
public sealed class ModulesEndpointsTests(ApiFactory factory)
{
    [Fact]
    public async Task Identity_provisions_external_subject_without_issuing_token()
    {
        var admin = factory.AuthenticatedClient();
        var subject = "external|" + Guid.NewGuid().ToString("N");
        var record = await admin.PostAsJsonAsync("/api/v1/identity/users", new
        {
            subject, userName = "Test", userEmail = "test@example.test"
        });
        record.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await record.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("subject").GetString().ShouldBe(subject);
        payload.TryGetProperty("accessToken", out _).ShouldBeFalse();
        var legacy = await admin.PostAsJsonAsync("/api/v1/identity/sessions", new { });
        legacy.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Event_remote_should_be_created_and_returned_without_local()
    {
        var client = factory.AuthenticatedClient();
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var response = await client.PostAsJsonAsync("/api/v1/events", new
        {
            eventName = $"Evento remoto {Guid.NewGuid():N}",
            eventStartDate = start,
            eventEndDate = start.AddHours(4),
            eventFormat = "Remote",
            eventRemoteUrl = "https://eventEntity.test.local/room",
            eventMaximumCapacity = 500,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/events/{created.GetProperty("id").GetGuid()}");
        detail.GetProperty("eventFormat").GetString().ShouldBe("Remote");
        if (detail.TryGetProperty("venueId", out var venueId)) venueId.ValueKind.ShouldBe(JsonValueKind.Null);
        detail.GetProperty("eventStatus").GetString().ShouldBe("Draft");
    }

    [Fact]
    public async Task Event_in_person_without_local_should_return_problem_details_of_business()
    {
        var client = factory.AuthenticatedClient();
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var response = await client.PostAsJsonAsync("/api/v1/events", new
        {
            eventName = $"Evento inválido {Guid.NewGuid():N}",
            eventStartDate = start,
            eventEndDate = start.AddHours(2),
            eventFormat = "InPerson",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await TestProblemDetails.ReadAsync(response);
        problem.Code.ShouldBe("Validation");
        problem.Errors!.Keys.ShouldContain("VenueId");
    }
}
