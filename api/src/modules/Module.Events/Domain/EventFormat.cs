namespace Module.Events.Domain;

/// <summary>Formato de realização do evento. Define quais dados de local/link são obrigatórios.</summary>
public enum EventFormat
{
    /// <summary>Exige <c>VenueId</c>.</summary>
    InPerson,

    /// <summary>Exige <c>EventRemoteUrl</c> e não admite <c>VenueId</c>.</summary>
    Remote,

    /// <summary>Exige <c>VenueId</c> e <c>EventRemoteUrl</c>.</summary>
    Hybrid,
}
