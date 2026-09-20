using Shared.Contracts.Venues;
using Shared.Data.Entities;
using Shared.Http.Results;

namespace Module.Venues.Domain;

/// <summary>
/// Agregado Local: onde eventos presenciais acontecem. Possui uma ou mais salas; um local de ambiente único
/// nasce com uma sala do tipo <see cref="RoomType.SingleRoom"/>.
/// </summary>
public sealed class Venue : BaseEntity
{
    private readonly List<Room> _rooms = [];

    private Venue()
    {
    }

    public string VenueName { get; private set; } = string.Empty;
    public string? VenueDescription { get; private set; }
    public string? AddressStreet { get; private set; }
    public string? AddressNumber { get; private set; }
    public string? AddressNeighborhood { get; private set; }
    public string AddressCity { get; private set; } = string.Empty;
    public string AddressState { get; private set; } = string.Empty;
    public string? AddressPostalCode { get; private set; }

    public IReadOnlyCollection<Room> Rooms => _rooms.AsReadOnly();

    public static Venue Create(
        string venueName,
        string? venueDescription,
        string? addressStreet,
        string? addressNumber,
        string? addressNeighborhood,
        string addressCity,
        string addressState,
        string? addressPostalCode,
        int? singleRoomCapacity)
    {
        var venue = new Venue();
        venue.Update(venueName, venueDescription, addressStreet, addressNumber, addressNeighborhood, addressCity, addressState, addressPostalCode);

        // Regra: local com apenas um ambiente é traduzido como uma sala.
        if (singleRoomCapacity is > 0)
        {
            venue._rooms.Add(new Room(venue.Id, "Ambiente único", singleRoomCapacity.Value, RoomType.SingleRoom, null));
        }

        venue.RecordEvent(new VenueCreated(venue.Id));
        return venue;
    }

    public void Update(
        string venueName,
        string? venueDescription,
        string? addressStreet,
        string? addressNumber,
        string? addressNeighborhood,
        string addressCity,
        string addressState,
        string? addressPostalCode)
    {
        VenueName = venueName.Trim();
        VenueDescription = venueDescription?.Trim();
        AddressStreet = addressStreet?.Trim();
        AddressNumber = addressNumber?.Trim();
        AddressNeighborhood = addressNeighborhood?.Trim();
        AddressCity = addressCity.Trim();
        AddressState = addressState.Trim().ToUpperInvariant();
        AddressPostalCode = addressPostalCode?.Trim();
    }

    public Result<Room> AddRoom(string roomName, int roomCapacity, RoomType roomType, string? roomResources)
    {
        if (_rooms.Any(s => s.DeletedAt is null && string.Equals(s.RoomName, roomName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return VenuesErrors.DuplicateRoomName;
        }

        var room = new Room(Id, roomName, roomCapacity, roomType, roomResources);
        _rooms.Add(room);
        return room;
    }

    public Result UpdateRoom(Guid roomId, string roomName, int roomCapacity, RoomType roomType, string? roomResources)
    {
        var room = _rooms.FirstOrDefault(s => s.Id == roomId && s.DeletedAt is null);
        if (room is null)
        {
            return VenuesErrors.RoomNotFound;
        }

        if (_rooms.Any(s => s.Id != roomId && s.DeletedAt is null && string.Equals(s.RoomName, roomName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return VenuesErrors.DuplicateRoomName;
        }

        room.Update(roomName, roomCapacity, roomType, roomResources);
        return Result.Success();
    }

    /// <summary>Retorna a sala a ser removida (soft delete feito pelo contexto). Um local nunca fica sem salas.</summary>
    public Result<Room> RemoveRoom(Guid roomId)
    {
        var room = _rooms.FirstOrDefault(s => s.Id == roomId && s.DeletedAt is null);
        if (room is null)
        {
            return VenuesErrors.RoomNotFound;
        }

        if (_rooms.Count(s => s.DeletedAt is null) <= 1)
        {
            return VenuesErrors.VenueRequiresRoom;
        }

        return room;
    }

    public void MarkDeleted() => RecordEvent(new VenueDeleted(Id));
}
