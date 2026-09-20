namespace Shared.Contracts.Identity;

/// <summary>O subject externo é opaco; nunca é interpretado como Guid interno.</summary>
public interface IIdentityResolver
{
    Task<IdentityAccount?> ResolveAsync(string issuer, string subject, CancellationToken ct);
}
public sealed record IdentityAccount(Guid Id, bool Enabled, DateTimeOffset TokensValidAfter);
