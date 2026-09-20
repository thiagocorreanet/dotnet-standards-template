using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Module.Events.Shared;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;

namespace Tests.Integration.Events;
[Collection(ApiCollection.Name)]
public sealed class ConsistencyAndOwnershipTests(ApiFactory factory)
{
    private static async Task<JsonElement> Created(HttpClient client, string path, object body)
    {
        var response = await client.PostAsJsonAsync(path, body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static Guid Id(JsonElement value) => value.GetProperty("id").GetGuid();
    private static async Task<Guid> Person(HttpClient client) => Id(await Created(client, "/api/v1/people",
        new { personName = "Synthetic Person", personEmail = Guid.NewGuid().ToString("N") + "@example.test" }));
    private async Task<HttpClient> Account(string role)
    {
        var subject = "opaque|" + Guid.NewGuid();
        await factory.CreateIdentityAsync(subject);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestToken.Generate(role, subject: subject));
        return client;
    }
    private async Task<Fixture> Setup(HttpClient client, int? capacity = null, bool talk = true)
    {
        var admin = factory.AuthenticatedClient();
        var location = Id(await Created(admin, "/api/v1/venues", new { venueName = "Local " + Guid.NewGuid(), addressCity = "City", addressState = "ES", singleRoomCapacity = 100 }));
        var venue = await client.GetFromJsonAsync<JsonElement>("/api/v1/venues/" + location);
        var room = Id(venue.GetProperty("rooms")[0]);
        var person = await Person(admin);
        var start = DateTimeOffset.UtcNow.AddDays(10);
        var eventId = Id(await Created(client, "/api/v1/events", new { eventName = "Evento " + Guid.NewGuid(), eventStartDate = start, eventEndDate = start.AddHours(8), eventFormat = "InPerson", venueId = location, eventMaximumCapacity = capacity }));
        var eventEntity = await client.GetFromJsonAsync<JsonElement>("/api/v1/events/" + eventId);
        var track = Id(eventEntity.GetProperty("tracks")[0]);
        var fixture = new Fixture(eventId, track, location, room, person, start, Guid.Empty);
        return talk ? fixture with { Talk = Id(await Created(client, "/api/v1/talks", TalkPayload(fixture, 1, 2))) } : fixture;
    }
    private static object TalkPayload(Fixture f, int from, int to) => new { eventId = f.Event, trackId = f.Track, roomId = f.Room, talkTitle = "Talk " + Guid.NewGuid(), talkStart = f.Start.AddHours(from), talkEnd = f.Start.AddHours(to), speakers = new[] { new { personId = f.Person, speakerRole = "Principal" } } };
    private static async Task Publish(HttpClient client, Guid id) =>
        (await client.PatchAsJsonAsync($"/api/v1/events/{id}/status", new { eventStatus = "Published" })).EnsureSuccessStatusCode();

    [Fact]
    public async Task Twenty_requests_competing_for_one_place_confirm_exactly_one()
    {
        var client = factory.AuthenticatedClient();
        var f = await Setup(client, 1);
        await Publish(client, f.Event);
        var people = new List<Guid>();
        for (var i = 0; i < 20; i++) people.Add(await Person(client));
        var results = await Task.WhenAll(people.Select(id => client.PostAsJsonAsync($"/api/v1/events/{f.Event}/registrations", new { personId = id })));
        results.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict || r.StatusCode == HttpStatusCode.UnprocessableEntity).ShouldBe(19);
        var count = await factory.WithServiceAsync(sp => sp.GetRequiredService<EventsDbContext>().Registrations.CountAsync(i => i.EventId == f.Event));
        count.ShouldBe(1);
    }

    [Fact]
    public async Task Concurrent_room_bookings_conflict_but_adjacent_intervals_work()
    {
        var client = factory.AuthenticatedClient();
        var f = await Setup(client, talk: false);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync("/api/v1/talks", TalkPayload(f, 1, 2))));
        results.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
        await Created(client, "/api/v1/talks", TalkPayload(f, 2, 3));
        // Defesa no banco protege também gravação que não passou pela API.
        var error = await Should.ThrowAsync<DbUpdateException>(() => factory.WithServiceAsync(async sp =>
        {
            var db = sp.GetRequiredService<TalksDbContext>();
            var duplicate = Talk.Create(f.Event, f.Track, f.Room, "Overlap", null, f.Start.AddHours(1), f.Start.AddHours(2),
                [new NewSpeaker(f.Person, SpeakerRole.Principal)]).Value;
            db.Talks.Add(duplicate);
            return await db.SaveChangesAsync();
        }));
        ((Npgsql.PostgresException)error.InnerException!).SqlState.ShouldBe("23P01");
    }

    [Fact]
    public async Task Users_cannot_manage_another_owners_resources_or_personal_records()
    {
        var owner = await Account("Organizer");
        var outsider = await Account("Organizer");
        var participant = await Account("Participant");
        var f = await Setup(owner);
        (await outsider.DeleteAsync($"/api/v1/events/{f.Event}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await outsider.DeleteAsync($"/api/v1/talks/{f.Talk}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await outsider.GetAsync($"/api/v1/events/{f.Event}/registrations")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await participant.GetAsync($"/api/v1/people/{f.Person}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await participant.PostAsJsonAsync($"/api/v1/events/{f.Event}/registrations", new { personId = f.Person })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await participant.PostAsJsonAsync($"/api/v1/talks/{f.Talk}/certificates", new { personId = f.Person })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var ownPerson = await Person(participant);
        (await participant.GetAsync($"/api/v1/people/{ownPerson}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Publish(owner, f.Event);
        (await participant.PostAsJsonAsync($"/api/v1/events/{f.Event}/registrations", new { personId = ownPerson })).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Concurrent_certificates_have_one_row_event_and_historical_snapshot()
    {
        var client = factory.AuthenticatedClient();
        var f = await Setup(client);
        // Fixture temporal: não altera o relógio global nem aguarda a palestra acabar.
        await factory.WithServiceAsync(async sp =>
        {
            var db = sp.GetRequiredService<TalksDbContext>();
            var talk = await db.Talks.SingleAsync(p => p.Id == f.Talk);
            talk.Update(f.Track, f.Room, "Original title", null, DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1));
            talk.RecordAttendance(f.Person, DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
            return await db.SaveChangesAsync();
        });
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.PostAsJsonAsync($"/api/v1/talks/{f.Talk}/certificates", new { personId = f.Person })));
        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(11);
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<JsonElement>()));
        var code = bodies[0].GetProperty("certificateCode").GetString()!;
        bodies.Select(b => b.GetProperty("certificateCode").GetString()).Distinct().Count().ShouldBe(1);
        await factory.WithServiceAsync(async sp =>
        {
            var db = sp.GetRequiredService<TalksDbContext>();
            (await db.Certificates.CountAsync(c => c.TalkId == f.Talk)).ShouldBe(1);
            var events = await db.OutboxMessages.Where(m => m.Type == "talks.certificate-issued.v1").Select(m => m.Payload).ToListAsync();
            events.Count(x => x.Contains(f.Talk.ToString(), StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
            var talk = await db.Talks.SingleAsync(p => p.Id == f.Talk);
            talk.Update(f.Track, f.Room, "Changed title", null, talk.TalkStart, talk.TalkEnd);
            return await db.SaveChangesAsync();
        });
        var publicResult = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/talks/certificates/" + code);
        publicResult.GetProperty("talkTitle").GetString().ShouldBe("Original title");
        publicResult.GetProperty("personName").GetString().ShouldBe("Titular verificado");
    }

    [Fact]
    public async Task Parallel_removal_keeps_one_track_and_one_room()
    {
        var client = factory.AuthenticatedClient();
        var f = await Setup(client, talk: false);
        var secondTrack = Id(await Created(client, $"/api/v1/events/{f.Event}/tracks", new { trackName = "Second" }));
        var results = await Task.WhenAll(new[] { f.Track, secondTrack }.Select(id => client.DeleteAsync($"/api/v1/events/{f.Event}/tracks/{id}")));
        results.Count(r => r.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
        results.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity).ShouldBe(1);
        // Um local não referenciado permite remoção, mas nunca da última sala.
        var venue = Id(await Created(client, "/api/v1/venues", new { venueName = "Independent " + Guid.NewGuid(), addressCity = "City", addressState = "ES", singleRoomCapacity = 10 }));
        var detail = await client.GetFromJsonAsync<JsonElement>("/api/v1/venues/" + venue);
        var first = Id(detail.GetProperty("rooms")[0]);
        var second = Id(await Created(client, $"/api/v1/venues/{venue}/rooms", new { roomName = "Second", roomCapacity = 10, roomType = "Laboratory" }));
        results = await Task.WhenAll(new[] { first, second }.Select(id => client.DeleteAsync($"/api/v1/venues/{venue}/rooms/{id}")));
        results.Count(r => r.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
        results.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity).ShouldBe(1);
    }

    [Fact]
    public async Task Referenced_location_schedule_and_last_published_talk_cannot_be_invalidated()
    {
        var client = factory.AuthenticatedClient();
        var f = await Setup(client);
        (await client.DeleteAsync($"/api/v1/venues/{f.Venue}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.PutAsJsonAsync($"/api/v1/events/{f.Event}", new { eventName = "Changed", eventStartDate = f.Start.AddDays(1), eventEndDate = f.Start.AddDays(2), eventFormat = "InPerson", venueId = f.Venue })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await Publish(client, f.Event);
        (await client.DeleteAsync($"/api/v1/talks/{f.Talk}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var changes = await Task.WhenAll(
            client.PatchAsJsonAsync($"/api/v1/events/{f.Event}/status", new { eventStatus = "InProgress" }),
            client.PatchAsJsonAsync($"/api/v1/events/{f.Event}/status", new { eventStatus = "Canceled", reason = "Synthetic cancellation" }));
        changes.Count(r => r.IsSuccessStatusCode).ShouldBeInRange(1, 2);
        var final = await client.GetFromJsonAsync<JsonElement>("/api/v1/events/" + f.Event);
        final.GetProperty("eventStatus").GetString().ShouldBe("Canceled");
    }
    [Fact]
    public async Task Venue_cannot_be_removed_while_a_talk_of_a_deleted_event_still_uses_its_room()
    {
        var client = factory.AuthenticatedClient();
        var f = await Setup(client);
        // Evento em rascunho pode ser excluído, mas a palestra permanece ativa apontando para a sala.
        (await client.DeleteAsync($"/api/v1/events/{f.Event}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var removal = await client.DeleteAsync($"/api/v1/venues/{f.Venue}");
        removal.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await TestProblemDetails.ReadAsync(removal)).Code.ShouldBe("Venues.ResourceInUse");
    }

    private sealed record Fixture(Guid Event, Guid Track, Guid Venue, Guid Room, Guid Person, DateTimeOffset Start, Guid Talk);
}
