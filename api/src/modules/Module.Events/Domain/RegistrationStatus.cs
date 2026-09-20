namespace Module.Events.Domain;

/// <summary>Situação de uma inscrição. O cancelamento não é soft delete: a inscrição permanece com situação <see cref="Canceled"/>.</summary>
public enum RegistrationStatus
{
    Confirmed,
    Canceled,
}
