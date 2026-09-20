using Shared.Data.Entities;
using Shared.Contracts.Identity;

namespace Module.Identity.Domain;

/// <summary>Vínculo local, sem senha e sem fonte paralela de perfis.</summary>
public sealed class User : BaseEntity
{
    private User() { }
    public string Issuer { get; private set; } = "";
    public string Subject { get; private set; } = "";
    public string UserName { get; private set; } = "";
    public string Email { get; private set; } = "";
    public DateTimeOffset TokensValidAfter { get; private set; } = DateTimeOffset.UnixEpoch;

    public static User Create(string issuer, string subject, string name, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        var user = new User { Issuer = issuer, Subject = subject, UserName = name.Trim(), Email = email.Trim() };
        user.RecordEvent(new UserRegistered(user.Id));
        return user;
    }

    public void UpdateAccess(bool active, DateTimeOffset revokeBefore)
    {
        IsActive = active;
        if (revokeBefore > TokensValidAfter) TokensValidAfter = revokeBefore;
    }
}
