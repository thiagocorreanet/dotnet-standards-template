using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Module.Venues.Shared;
using Shared.Contracts.Identity;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;

namespace Tests.Integration.Venues;

[Collection(ApiCollection.Name)]
public sealed class VenuesEndpointsTests(ApiFactory factory)
{
    private static object NewVenue(string name, int? capacity = 120) => new
    {
        venueName = name,
        venueDescription = "Criado em teste de integração",
        addressCity = "Vila Velha",
        addressState = "es",
        singleRoomCapacity = capacity,
    };

    [Fact]
    public async Task Without_token_should_return_401_problem_details()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/venues");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Participant_not_can_create_local_403()
    {
        var client = factory.AuthenticatedClient(DefaultRoles.Participant);
        var response = await client.PostAsJsonAsync("/api/v1/venues", NewVenue($"Local {Guid.NewGuid():N}"));
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_local_with_environment_single_generates_a_room_and_records_audit()
    {
        var client = factory.AuthenticatedClient();
        var name = $"Auditório {Guid.NewGuid():N}";
        var start = DateTimeOffset.UtcNow.AddSeconds(-5);

        var created = await client.PostAsJsonAsync("/api/v1/venues", NewVenue(name));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        created.Headers.Location.ShouldNotBeNull();
        created.Headers.GetValues("X-Correlation-Id").ShouldNotBeEmpty();
        var body = await created.Content.ReadFromJsonAsync<CreatedResponse>();
        body!.RoomsCount.ShouldBe(1);

        var detail = await client.GetFromJsonAsync<VenueDetail>(created.Headers.Location);
        detail!.VenueName.ShouldBe(name);
        detail.AddressState.ShouldBe("ES");
        detail.Rooms.Count.ShouldBe(1);
        detail.Rooms[0].RoomType.ShouldBe("SingleRoom");
        detail.VenueTotalCapacity.ShouldBe(120);

        // Campos de auditoria preenchidos pelo interceptor e Outbox gravado na mesma transação
        var (createdBy, messages) = await factory.WithServiceAsync(async sp =>
        {
            var db = sp.GetRequiredService<VenuesDbContext>();
            var venue = await db.Venues.TagWith("Tests.Venues.Audit").AsNoTracking().FirstAsync(l => l.Id == body.Id);
            // Payload é jsonb: filtra por período no banco e pelo id em memória (tabela pequena no teste).
            var recent = await db.OutboxMessages.TagWith("Tests.Venues.Outbox").AsNoTracking()
                .Where(m => m.OccurredOn >= start).Select(m => new { m.Type, m.Payload }).ToListAsync();
            var msgs = recent.Where(m => m.Payload.Contains(body.Id.ToString(), StringComparison.OrdinalIgnoreCase)).Select(m => m.Type).ToList();
            return (venue.CreatedBy, msgs);
        });
        createdBy.ShouldBe(factory.DefaultAccountId.ToString());
        messages.ShouldContain("venues.venue-created.v1");
        messages.ShouldContain("audit.entity-changed.v1");
    }

    [Fact]
    public async Task Name_duplicate_should_return_409_with_code()
    {
        var client = factory.AuthenticatedClient();
        var name = $"Duplicado {Guid.NewGuid():N}";
        (await client.PostAsJsonAsync("/api/v1/venues", NewVenue(name))).EnsureSuccessStatusCode();

        var repeated = await client.PostAsJsonAsync("/api/v1/venues", NewVenue(name));
        repeated.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await TestProblemDetails.ReadAsync(repeated);
        problem.Code.ShouldBe("Venues.DuplicateVenueName");
        problem.TraceId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Payload_invalid_should_return_400_with_errors_by_field()
    {
        var client = factory.AuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/v1/venues", new { venueName = "", addressCity = "", addressState = "ESP" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await TestProblemDetails.ReadAsync(response);
        problem.Code.ShouldBe("Validation");
        problem.Errors!.Keys.ShouldContain("VenueName");
        problem.Errors.Keys.ShouldContain("AddressState");
    }

    [Fact]
    public async Task Not_should_remove_the_last_room_422_and_soft_delete_of_room_extra()
    {
        var client = factory.AuthenticatedClient();
        var created = await client.PostAsJsonAsync("/api/v1/venues", NewVenue($"Salas {Guid.NewGuid():N}"));
        var venue = (await created.Content.ReadFromJsonAsync<CreatedResponse>())!;

        var room = await client.PostAsJsonAsync($"/api/v1/venues/{venue.Id}/rooms", new { roomName = "Lab 1", roomCapacity = 30, roomType = "Laboratory" });
        room.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdRoom = (await room.Content.ReadFromJsonAsync<RoomResponse>())!;

        (await client.DeleteAsync($"/api/v1/venues/{venue.Id}/rooms/{createdRoom.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await client.GetFromJsonAsync<VenueDetail>($"/api/v1/venues/{venue.Id}");
        detail!.Rooms.Count.ShouldBe(1);
        var last = await client.DeleteAsync($"/api/v1/venues/{venue.Id}/rooms/{detail.Rooms[0].Id}");
        last.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await TestProblemDetails.ReadAsync(last)).Code.ShouldBe("Venues.VenueRequiresRoom");

        var deleted = await factory.WithServiceAsync(async sp =>
        {
            var db = sp.GetRequiredService<VenuesDbContext>();
            return await db.Rooms.TagWith("Tests.Venues.SoftDelete").IgnoreQueryFilters([Shared.Data.ModuleDbContext.SoftDeleteFilterName])
                .AsNoTracking().Where(s => s.Id == createdRoom.Id).Select(s => new { s.DeletedAt, s.DeletedBy, s.IsActive }).FirstAsync();
        });
        deleted.DeletedAt.ShouldNotBeNull();
        deleted.DeletedBy.ShouldBe(factory.DefaultAccountId.ToString());
        deleted.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Listing_is_paginated_and_filters_by_search()
    {
        var client = factory.AuthenticatedClient();
        var marker = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 3; i++)
        {
            (await client.PostAsJsonAsync("/api/v1/venues", NewVenue($"Pag {marker} {i}"))).EnsureSuccessStatusCode();
        }

        var page = await client.GetFromJsonAsync<Paged<VenueItem>>($"/api/v1/venues?search={marker}&page=1&pageSize=2&sortBy=venueName&direction=Desc");
        page!.Total.ShouldBe(3);
        page.Items.Count.ShouldBe(2);
        page.TotalPages.ShouldBe(2);
        page.Items.Select(x => x.VenueName).ShouldBeInOrder(SortDirection.Descending);
    }

    [Fact]
    public async Task Listing_rejects_unsupported_sort_field()
    {
        var response = await factory.AuthenticatedClient().GetAsync("/api/v1/venues?sortBy=sqlInjection");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await TestProblemDetails.ReadAsync(response)).Code.ShouldBe("Validation");
    }

    [Fact]
    public async Task OpenApi_should_expose_a_single_tag_by_module_and_yaml()
    {
        var client = factory.CreateClient();
        var json = await client.GetStringAsync("/openapi/v1.json");
        json.ShouldContain("\"Venues\"");
        var yaml = await client.GetStringAsync("/openapi/v1.yaml");
        yaml.ShouldStartWith("openapi:");
    }

    private sealed record CreatedResponse(Guid Id, string VenueName, int RoomsCount);
    private sealed record RoomResponse(Guid Id, Guid VenueId, string RoomName);
    private sealed record RoomDetail(Guid Id, string RoomName, int RoomCapacity, string RoomType, bool IsActive);
    private sealed record VenueDetail(Guid Id, string VenueName, string AddressState, int VenueTotalCapacity, List<RoomDetail> Rooms);
    private sealed record VenueItem(Guid Id, string VenueName);
    private sealed record Paged<T>(List<T> Items, int Page, int PageSize, long Total, int TotalPages);
}
