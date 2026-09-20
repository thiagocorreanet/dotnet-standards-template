using Shared.Http.Results;

namespace Module.Venues.Domain;

public static class VenuesErrors
{
    public static readonly Error VenueNotFound = Error.NotFound("Venues.VenueNotFound", "Local não encontrado.");
    public static readonly Error DuplicateVenueName = Error.Conflict("Venues.DuplicateVenueName", "Já existe um local com este nome.");
    public static readonly Error RoomNotFound = Error.NotFound("Venues.RoomNotFound", "Sala não encontrada neste local.");
    public static readonly Error DuplicateRoomName = Error.Conflict("Venues.DuplicateRoomName", "Já existe uma sala com este nome neste local.");
    public static readonly Error VenueRequiresRoom = Error.BusinessRule("Venues.VenueRequiresRoom", "Um local precisa manter ao menos uma sala (ambiente).");
}
