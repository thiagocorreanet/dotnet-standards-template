using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Module.Identity.Domain;
using Shared.Contracts.Identity;
using Shared.WebHost.Security;
namespace Module.Identity.Shared;
internal sealed class IdentityBootstrapper(IdentityDbContext db, IOptions<OidcOptions> options) : IIdentityBootstrapper
{
    public async Task<Guid> ProvisionFirstAsync(string subject, string name, string email, CancellationToken ct)
    {
        var request = new UseCases.RegisterUser.RegisterUserRequest(subject, name, email);
        var validation = await new UseCases.RegisterUser.RegisterUserValidator().ValidateAsync(request, ct);
        if (!validation.IsValid) throw new InvalidOperationException("Subject, nome ou e-mail inválido.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var lockId = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes("identity")));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", ct);
        if (await db.Users.IgnoreQueryFilters().AnyAsync(ct))
            throw new InvalidOperationException("Bootstrap permitido somente em uma instalação sem vínculos. Use o endpoint administrativo.");
        var account = User.Create(options.Value.Authority, subject, name, email);
        db.Users.Add(account);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return account.Id;
    }
}
