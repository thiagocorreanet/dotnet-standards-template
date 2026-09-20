using Shared.Data.Entities;

namespace Module.Events.Domain;

/// <summary>Inscrição de uma pessoa em um evento. Entidade filha do agregado <see cref="Event"/>.</summary>
public sealed class Registration : BaseEntity
{
    private Registration()
    {
    }

    internal Registration(Guid eventId, Guid personId, DateTimeOffset registeredAt)
    {
        EventId = eventId;
        PersonId = personId;
        RegistrationStatus = RegistrationStatus.Confirmed;
        RegistrationRegisteredAt = registeredAt;
    }

    public Guid EventId { get; private set; }
    public Guid PersonId { get; private set; }
    public RegistrationStatus RegistrationStatus { get; private set; }
    public DateTimeOffset RegistrationRegisteredAt { get; private set; }
    public DateTimeOffset? RegistrationCanceledAt { get; private set; }

    public Event Event { get; private set; } = null!;

    public bool IsConfirmed => RegistrationStatus == RegistrationStatus.Confirmed;

    internal void Cancel(DateTimeOffset canceledAt)
    {
        RegistrationStatus = RegistrationStatus.Canceled;
        RegistrationCanceledAt = canceledAt;
    }
}
