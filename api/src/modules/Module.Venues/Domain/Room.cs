using Shared.Data.Entities;

namespace Module.Venues.Domain;

/// <summary>Ambiente de um local: auditório, sala de aula, área de recreação etc. É a unidade alocável para palestras.</summary>
public sealed class Room : BaseEntity
{
    private Room()
    {
    }

    internal Room(Guid venueId, string roomName, int roomCapacity, RoomType roomType, string? roomResources)
    {
        VenueId = venueId;
        RoomName = roomName.Trim();
        RoomCapacity = roomCapacity;
        RoomType = roomType;
        RoomResources = roomResources?.Trim();
    }

    public Guid VenueId { get; private set; }
    public string RoomName { get; private set; } = string.Empty;
    public int RoomCapacity { get; private set; }
    public RoomType RoomType { get; private set; }

    /// <summary>Descrição livre de recursos (ex.: "projetor, ar-condicionado, 40 tomadas").</summary>
    public string? RoomResources { get; private set; }

    public Venue Venue { get; private set; } = null!;

    internal void Update(string roomName, int roomCapacity, RoomType roomType, string? roomResources)
    {
        RoomName = roomName.Trim();
        RoomCapacity = roomCapacity;
        RoomType = roomType;
        RoomResources = roomResources?.Trim();
    }
}
