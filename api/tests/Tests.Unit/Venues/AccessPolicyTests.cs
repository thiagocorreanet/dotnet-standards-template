using Module.Venues.Domain;
using Module.Venues.UseCases.AddRoom;
using Module.Venues.UseCases.CreateVenue;
using Module.Venues.UseCases.DeleteRoom;
using Module.Venues.UseCases.DeleteVenue;
using Module.Venues.UseCases.GetVenue;
using Module.Venues.UseCases.ListRooms;
using Module.Venues.UseCases.ListVenues;
using Module.Venues.UseCases.UpdateRoom;
using Module.Venues.UseCases.UpdateVenue;
using Shouldly;
using Tests.Unit.Shared;

namespace Tests.Unit.Venues;

public sealed class AccessPolicyTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Theory]
    [InlineData("anonymous", false)]
    [InlineData("unbound", true)]
    [InlineData("participant", true)]
    public async Task Any_authenticated_user_reads_venues_and_rooms(string profile, bool allowed)
    {
        var user = TestUsers.For(profile);

        (await new ListVenuesAccessPolicy(user).CanExecuteAsync(new ListVenuesRequest(null, null, null), Ct)).ShouldBe(allowed);
        (await new GetVenueAccessPolicy(user).CanExecuteAsync(new GetVenueRequest(Guid.NewGuid()), Ct)).ShouldBe(allowed);
        (await new ListRoomsAccessPolicy(user).CanExecuteAsync(new ListRoomsRequest(Guid.NewGuid(), null), Ct)).ShouldBe(allowed);
    }

    [Theory]
    [InlineData("anonymous", false)]
    [InlineData("participant", false)]
    [InlineData("organizer", false)]
    [InlineData("administrator", true)]
    public async Task Only_administrator_changes_venues_and_rooms(string profile, bool allowed)
    {
        var user = TestUsers.For(profile);
        var venueId = Guid.NewGuid();

        (await new CreateVenueAccessPolicy(user).CanExecuteAsync(new CreateVenueRequest("Local", null, null, null, null, "Cidade", "SP", null, null), Ct))
            .ShouldBe(allowed);
        (await new UpdateVenueAccessPolicy(user).CanExecuteAsync(new UpdateVenueRequest("Local", null, null, null, null, "Cidade", "SP", null) { VenueId = venueId }, Ct))
            .ShouldBe(allowed);
        (await new DeleteVenueAccessPolicy(user).CanExecuteAsync(new DeleteVenueRequest(venueId), Ct)).ShouldBe(allowed);
        (await new AddRoomAccessPolicy(user).CanExecuteAsync(new AddRoomRequest("Sala", 10, RoomType.Auditorium, null) { VenueId = venueId }, Ct))
            .ShouldBe(allowed);
        (await new UpdateRoomAccessPolicy(user).CanExecuteAsync(new UpdateRoomRequest("Sala", 10, RoomType.Auditorium, null, true) { VenueId = venueId }, Ct))
            .ShouldBe(allowed);
        (await new DeleteRoomAccessPolicy(user).CanExecuteAsync(new DeleteRoomRequest(venueId, Guid.NewGuid()), Ct)).ShouldBe(allowed);
    }
}
