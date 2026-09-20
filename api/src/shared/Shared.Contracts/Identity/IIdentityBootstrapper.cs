namespace Shared.Contracts.Identity;
/// <summary>Provisionamento inicial explícito via CLI; nunca executado na subida normal.</summary>
public interface IIdentityBootstrapper
{
    Task<Guid> ProvisionFirstAsync(string subject, string name, string email, CancellationToken ct);
}
