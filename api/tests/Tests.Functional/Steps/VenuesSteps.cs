using System.Net;
using Reqnroll;
using Shouldly;

namespace Tests.Functional.Steps;

[Binding]
public sealed class VenuesSteps(ScenarioContext context)
{
    [Given("que existe um local chamado {string} com ambiente único para {int} pessoas")]
    [When("crio um local chamado {string} com ambiente único para {int} pessoas")]
    public async Task WhenICreateASingleRoomVenue(string name, int capacity)
    {
        var response = await context.PostAsync("/api/v1/venues", new
        {
            venueName = context.UniqueName(name),
            addressCity = "Vila Velha",
            addressState = "ES",
            singleRoomCapacity = capacity,
        });
        if (response.StatusCode == HttpStatusCode.Created)
        {
            var body = await context.JsonBodyAsync();
            context.Ids["venue"] = body.GetProperty("id").GetGuid();
        }
    }

    [When("tento criar outro local chamado {string} com ambiente único para {int} pessoas")]
    public async Task TryToCreateDuplicateVenue(string name, int capacity) => await WhenICreateASingleRoomVenue(name, capacity);

    [When("tento criar um local com UF {string}")]
    public async Task TryToCreateVenueWithState(string state) => await context.PostAsync("/api/v1/venues", new
    {
        venueName = context.UniqueName("Local UF inválida"), addressCity = "Vila Velha", addressState = state, singleRoomCapacity = 10,
    });

    [When("excluo o local")]
    public async Task DeleteVenue() => await context.DeleteAsync($"/api/v1/venues/{context.Ids["venue"]}");

    [Then("o local não deve mais ser encontrado")]
    public async Task VenueNotFound()
    {
        await context.GetAsync($"/api/v1/venues/{context.Ids["venue"]}");
        context.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [When("adiciono a sala {string} com capacidade {int} do tipo {string}")]
    public async Task WhenIAddTheRoom(string name, int capacity, string type)
    {
        var response = await context.PostAsync($"/api/v1/venues/{context.Ids["venue"]}/rooms", new { roomName = name, roomCapacity = capacity, roomType = type });
        if (response.StatusCode == HttpStatusCode.Created)
        {
            context.Ids[$"room:{name}"] = (await context.JsonBodyAsync()).GetProperty("id").GetGuid();
        }
    }

    [When("tento excluir a sala {string}")]
    public async Task WhenITryToDeleteTheRoom(string name)
    {
        if (!context.Ids.TryGetValue($"room:{name}", out var roomId))
        {
            await context.GetAsync($"/api/v1/venues/{context.Ids["venue"]}");
            var rooms = (await context.JsonBodyAsync()).GetProperty("rooms").EnumerateArray();
            roomId = rooms.First(s => s.GetProperty("roomName").GetString() == name).GetProperty("id").GetGuid();
        }

        await context.DeleteAsync($"/api/v1/venues/{context.Ids["venue"]}/rooms/{roomId}");
    }

    [Then("o local deve possuir {int} sala\\(s\\)")]
    public async Task ThenTheVenueShouldHaveRooms(int count)
    {
        await context.GetAsync($"/api/v1/venues/{context.Ids["venue"]}");
        var body = await context.JsonBodyAsync();
        body.GetProperty("rooms").GetArrayLength().ShouldBe(count);
    }

    [Then("a capacidade total do local deve ser {int}")]
    public async Task ThenTotalCapacityShouldBe(int capacity)
    {
        await context.GetAsync($"/api/v1/venues/{context.Ids["venue"]}");
        (await context.JsonBodyAsync()).GetProperty("venueTotalCapacity").GetInt32().ShouldBe(capacity);
    }

    [Then("a primeira sala deve se chamar {string} e ser do tipo {string}")]
    public async Task ThenTheFirstRoomShouldBeNamed(string name, string type)
    {
        await context.GetAsync($"/api/v1/venues/{context.Ids["venue"]}");
        var room = (await context.JsonBodyAsync()).GetProperty("rooms")[0];
        room.GetProperty("roomName").GetString().ShouldBe(name);
        room.GetProperty("roomType").GetString().ShouldBe(type);
    }
}
