using Module.Venues.Domain;
using Shared.Contracts.Venues;
using Shouldly;

namespace Tests.Unit.Venues;

public sealed class VenueTests
{
    private static Venue NewVenue(int? capacity = 100) =>
        Venue.Create("Auditório", null, null, null, null, "Vila Velha", "es", null, capacity);

    [Fact]
    public void Venue_of_environment_single_starts_with_a_room_and_records_event()
    {
        var venue = NewVenue(150);
        venue.Rooms.Count.ShouldBe(1);
        venue.Rooms.Single().RoomType.ShouldBe(RoomType.SingleRoom);
        venue.Rooms.Single().RoomCapacity.ShouldBe(150);
        venue.AddressState.ShouldBe("ES");
        venue.Events.ShouldHaveSingleItem().ShouldBeOfType<VenueCreated>();
        venue.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Not_should_allow_room_with_name_duplicate()
    {
        var venue = NewVenue();
        var result = venue.AddRoom("ambiente único", 10, RoomType.Classroom, null);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(VenuesErrors.DuplicateRoomName);
    }

    [Fact]
    public void Not_should_remove_the_last_room()
    {
        var venue = NewVenue();
        var result = venue.RemoveRoom(venue.Rooms.Single().Id);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(VenuesErrors.VenueRequiresRoom);
    }

    [Fact]
    public void Should_remove_room_when_there_are_more_of_a()
    {
        var venue = NewVenue();
        var room = venue.AddRoom("Lab", 20, RoomType.Laboratory, "projetor").Value;
        venue.RemoveRoom(room.Id).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Room_missing_should_return_not_found()
    {
        var venue = NewVenue();
        venue.UpdateRoom(Guid.NewGuid(), "X", 1, RoomType.Other, null).Error.ShouldBe(VenuesErrors.RoomNotFound);
    }
}
