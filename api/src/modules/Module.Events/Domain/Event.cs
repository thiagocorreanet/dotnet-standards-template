using Shared.Contracts.Events;
using Shared.Data.Entities;
using Shared.Http.Results;

namespace Module.Events.Domain;

/// <summary>
/// Agregado Evento: dados básicos, formato (presencial/remoto/híbrido), máquina de estados da situação e inscrições.
/// Toda regra de negócio devolve <see cref="Result"/>; eventos de integração são registrados aqui e gravados no Outbox pelo contexto.
/// </summary>
public sealed class Event : BaseEntity
{
    private readonly List<Registration> _registrations = [];
    private readonly List<Track> _tracks = [];

    public Guid OrganizerId { get; private set; }
    public void SetOrganizer(Guid userId) => OrganizerId = userId;

    private Event()
    {
    }

    public string EventName { get; private set; } = string.Empty;
    public string? EventDescription { get; private set; }
    public DateTimeOffset EventStartDate { get; private set; }
    public DateTimeOffset EventEndDate { get; private set; }
    public EventFormat EventFormat { get; private set; }
    public Guid? VenueId { get; private set; }
    public string? EventRemoteUrl { get; private set; }
    public EventStatus EventStatus { get; private set; }
    public int? EventMaximumCapacity { get; private set; }
    public string? EventCancellationReason { get; private set; }

    public IReadOnlyCollection<Registration> Registrations => _registrations.AsReadOnly();
    public IReadOnlyCollection<Track> Tracks => _tracks.AsReadOnly();

    public Result<Track> AddTrack(string trackName, string? trackDescription, string? trackColor)
    {
        if (_tracks.Any(t => t.DeletedAt is null && string.Equals(t.TrackName, trackName.Trim(), StringComparison.OrdinalIgnoreCase)))
            return EventsErrors.DuplicateTrackName;

        var track = new Track(Id, trackName, trackDescription, trackColor);
        _tracks.Add(track);
        return track;
    }

    public Result UpdateTrack(Guid trackId, string trackName, string? trackDescription, string? trackColor)
    {
        var track = _tracks.FirstOrDefault(t => t.Id == trackId && t.DeletedAt is null);
        if (track is null) return EventsErrors.TrackNotFound;
        if (_tracks.Any(t => t.Id != trackId && t.DeletedAt is null && string.Equals(t.TrackName, trackName.Trim(), StringComparison.OrdinalIgnoreCase)))
            return EventsErrors.DuplicateTrackName;
        track.Update(trackName, trackDescription, trackColor);
        return Result.Success();
    }

    public Result<Track> RemoveTrack(Guid trackId)
    {
        var track = _tracks.FirstOrDefault(t => t.Id == trackId && t.DeletedAt is null);
        if (track is null) return EventsErrors.TrackNotFound;
        if (_tracks.Count(t => t.DeletedAt is null) <= 1) return EventsErrors.EventRequiresTrack;
        return track;
    }

    /// <summary>Evento nasce em <see cref="EventStatus.Draft"/>. Falha com <c>InconsistentFormat</c> se local/link não combinam com o formato.</summary>
    public static Result<Event> Create(
        string eventName,
        string? eventDescription,
        DateTimeOffset eventStartDate,
        DateTimeOffset eventEndDate,
        EventFormat eventFormat,
        Guid? venueId,
        string? eventRemoteUrl,
        int? eventMaximumCapacity)
    {
        var eventEntity = new Event { EventStatus = EventStatus.Draft };
        var result = eventEntity.ApplyData(eventName, eventDescription, eventStartDate, eventEndDate, eventFormat, venueId, eventRemoteUrl, eventMaximumCapacity);
        return result.IsFailure ? result.Error : eventEntity;
    }

    /// <summary>Atualização permitida apenas em Rascunho ou Publicado (<c>EventCannotBeUpdated</c>).</summary>
    public Result Update(
        string eventName,
        string? eventDescription,
        DateTimeOffset eventStartDate,
        DateTimeOffset eventEndDate,
        EventFormat eventFormat,
        Guid? venueId,
        string? eventRemoteUrl,
        int? eventMaximumCapacity)
    {
        if (!CanBeUpdated)
        {
            return EventsErrors.EventCannotBeUpdated;
        }

        return ApplyData(eventName, eventDescription, eventStartDate, eventEndDate, eventFormat, venueId, eventRemoteUrl, eventMaximumCapacity);
    }

    public bool CanBeUpdated => EventStatus is EventStatus.Draft or EventStatus.Published;

    public bool CanBeDeleted => EventStatus is EventStatus.Draft or EventStatus.Canceled;

    public bool AcceptsRegistrations => EventStatus is EventStatus.Published or EventStatus.InProgress;

    /// <summary>Rascunho → Publicado. Exige ao menos uma palestra (contada pelo módulo Palestras). Emite <see cref="EventPublished"/>.</summary>
    public Result Publish(int talkCount)
    {
        if (EventStatus != EventStatus.Draft)
        {
            return EventsErrors.InvalidStatusTransition;
        }

        if (talkCount < 1)
        {
            return EventsErrors.EventWithoutTalks;
        }

        EventStatus = EventStatus.Published;
        RecordEvent(new EventPublished(Id));
        return Result.Success();
    }

    /// <summary>Publicado → EmAndamento.</summary>
    public Result Start()
    {
        if (EventStatus != EventStatus.Published)
        {
            return EventsErrors.InvalidStatusTransition;
        }

        EventStatus = EventStatus.InProgress;
        return Result.Success();
    }

    /// <summary>Publicado|EmAndamento → Encerrado.</summary>
    public Result Close()
    {
        if (EventStatus is not (EventStatus.Published or EventStatus.InProgress))
        {
            return EventsErrors.InvalidStatusTransition;
        }

        EventStatus = EventStatus.Closed;
        return Result.Success();
    }

    /// <summary>Rascunho|Publicado|EmAndamento → Cancelado, com motivo obrigatório. Emite <see cref="EventCanceled"/>.</summary>
    public Result Cancel(string? reason)
    {
        if (EventStatus is not (EventStatus.Draft or EventStatus.Published or EventStatus.InProgress))
        {
            return EventsErrors.InvalidStatusTransition;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return EventsErrors.CancellationReasonRequired;
        }

        EventStatus = EventStatus.Canceled;
        EventCancellationReason = reason.Trim();
        RecordEvent(new EventCanceled(Id));
        return Result.Success();
    }

    /// <summary>Exclusão lógica permitida apenas em Rascunho ou Cancelado (<c>EventCannotBeDeleted</c>).</summary>
    public Result MarkDeleted() => CanBeDeleted ? Result.Success() : EventsErrors.EventCannotBeDeleted;

    /// <summary>
    /// Capacidade efetiva: <see cref="EventMaximumCapacity"/> ou, quando nula e há local, a capacidade total do local.
    /// <c>null</c> significa sem limite.
    /// </summary>
    public int? EffectiveCapacity(int? venueTotalCapacity) =>
        EventMaximumCapacity ?? (VenueId.HasValue ? venueTotalCapacity : null);

    /// <summary>
    /// Inscreve uma pessoa. <paramref name="confirmedRegistrations"/> é a contagem atual (consulta projetada);
    /// <paramref name="venueTotalCapacity"/> vem do <c>VenueSummary</c> quando há local. Emite <see cref="RegistrationCompleted"/>.
    /// A unicidade por pessoa é garantida também por índice único filtrado no banco.
    /// </summary>
    public Result<Registration> Register(Guid personId, int confirmedRegistrations, int? venueTotalCapacity, DateTimeOffset now)
    {
        if (!AcceptsRegistrations)
        {
            return EventsErrors.EventDoesNotAcceptRegistrations;
        }

        if (_registrations.Any(i => i.PersonId == personId && i.IsConfirmed))
        {
            return EventsErrors.PersonAlreadyRegistered;
        }

        var capacity = EffectiveCapacity(venueTotalCapacity);
        if (capacity.HasValue && confirmedRegistrations >= capacity.Value)
        {
            return EventsErrors.CapacityExhausted;
        }

        var registration = new Registration(Id, personId, now);
        _registrations.Add(registration);
        RecordEvent(new RegistrationCompleted(registration.Id, Id, personId));
        return registration;
    }

    /// <summary>Cancela uma inscrição carregada na coleção (não é soft delete). Emite <see cref="RegistrationCanceled"/>.</summary>
    public Result<Registration> CancelRegistration(Guid registrationId, DateTimeOffset now)
    {
        var registration = _registrations.FirstOrDefault(i => i.Id == registrationId);
        if (registration is null)
        {
            return EventsErrors.RegistrationNotFound;
        }

        if (!registration.IsConfirmed)
        {
            return EventsErrors.RegistrationAlreadyCanceled;
        }

        registration.Cancel(now);
        RecordEvent(new RegistrationCanceled(registration.Id, Id, registration.PersonId));
        return registration;
    }

    /// <summary>Consistência entre formato e dados de local/link: Presencial exige local; Remoto exige link e não admite local; Híbrido exige ambos.</summary>
    public static bool IsFormatConsistent(EventFormat format, Guid? venueId, string? eventRemoteUrl)
    {
        var hasVenue = venueId.HasValue && venueId.Value != Guid.Empty;
        var hasLink = !string.IsNullOrWhiteSpace(eventRemoteUrl);
        return format switch
        {
            EventFormat.InPerson => hasVenue,
            EventFormat.Remote => hasLink && !hasVenue,
            EventFormat.Hybrid => hasVenue && hasLink,
            _ => false,
        };
    }

    private Result ApplyData(
        string eventName,
        string? eventDescription,
        DateTimeOffset eventStartDate,
        DateTimeOffset eventEndDate,
        EventFormat eventFormat,
        Guid? venueId,
        string? eventRemoteUrl,
        int? eventMaximumCapacity)
    {
        if (!IsFormatConsistent(eventFormat, venueId, eventRemoteUrl))
        {
            return EventsErrors.InconsistentFormat;
        }

        EventName = eventName.Trim();
        EventDescription = string.IsNullOrWhiteSpace(eventDescription) ? null : eventDescription.Trim();
        EventStartDate = eventStartDate;
        EventEndDate = eventEndDate;
        EventFormat = eventFormat;
        VenueId = venueId;
        EventRemoteUrl = string.IsNullOrWhiteSpace(eventRemoteUrl) ? null : eventRemoteUrl.Trim();
        EventMaximumCapacity = eventMaximumCapacity;
        return Result.Success();
    }
}
