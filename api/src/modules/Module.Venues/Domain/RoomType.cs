namespace Module.Venues.Domain;

/// <summary>Tipos de ambiente de um local. Um local com um único ambiente é representado por uma sala do tipo <see cref="SingleRoom"/>.</summary>
public enum RoomType
{
    SingleRoom,
    Auditorium,
    Classroom,
    Laboratory,
    RecreationArea,
    Coworking,
    Other,
}
