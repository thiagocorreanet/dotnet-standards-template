using Microsoft.EntityFrameworkCore;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Contracts.People;
using Shared.Http.Endpoints;
using Shared.Http.Results;
namespace Module.Talks.UseCases.ValidateCertificate;
/// <summary>Snapshot histórico. Na consulta anônima o nome do titular não é divulgado.</summary>
internal sealed class ValidateCertificateUseCase(TalksDbContext db, ICurrentUser user, IPeopleModuleApi people)
    : IUseCase<ValidateCertificateRequest, ValidateCertificateResponse>
{
    public async Task<Result<ValidateCertificateResponse>> HandleAsync(ValidateCertificateRequest request, CancellationToken ct)
    {
        if (request.CertificateCode.Length > 32) return TalksErrors.CertificateNotFound;
        var code = request.CertificateCode.Trim().ToUpperInvariant();
        var c = await db.Certificates.AsNoTracking().SingleOrDefaultAsync(c => c.CertificateCode == code, ct);
        if (c is null) return TalksErrors.CertificateNotFound;
        var canSeeName = user.HasRole(DefaultRoles.Administrator) ||
            user.Id.HasValue && await people.BelongsToUserAsync(c.PersonId, user.Id.Value, ct);
        return new ValidateCertificateResponse(c.CertificateCode, c.TalkTitleSnapshot, c.EventNameSnapshot,
            canSeeName ? c.PersonNameSnapshot : "Titular verificado", c.TalkStartSnapshot, c.CertificateIssuedAt, c.CertificateDurationMinutes);
    }
}
