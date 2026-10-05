using Shared.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Contracts.Talks;
using Shared.Contracts.People;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.Talks.UseCases.IssueCertificate;

/// <summary>Emite (ou devolve o já emitido) o certificado de uma pessoa com presença na palestra encerrada. Emite <see cref="CertificateIssued"/> quando cria.</summary>
[Command("event-management-example")]
internal sealed class IssueCertificateUseCase(TalksDbContext db, IPeopleModuleApi peopleApi, IEventsModuleApi eventsApi, TimeProvider timeProvider, ILogger<IssueCertificateUseCase> logger) : IUseCase<IssueCertificateRequest, IssueCertificateResponse>
{
    public async Task<Result<IssueCertificateResponse>> HandleAsync(IssueCertificateRequest request, CancellationToken cancellationToken)
    {
        var talk = await db.Talks
            .TagWith("Talks.IssueCertificate.Load")
            .Include(p => p.Attendances.Where(x => x.PersonId == request.PersonId))
            .Include(p => p.Certificates.Where(x => x.PersonId == request.PersonId))
            .FirstOrDefaultAsync(p => p.Id == request.TalkId, cancellationToken);
        if (talk is null)
        {
            return TalksErrors.TalkNotFound;
        }

        var result = talk.IssueCertificate(request.PersonId, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return result.Error;
        }

        var (certificate, created) = result.Value;
        if (created)
        {
            var owner = await peopleApi.GetPersonSummaryAsync(request.PersonId, cancellationToken);
            var eventEntity = await eventsApi.GetEventSummaryAsync(talk.EventId, cancellationToken);
            if (owner is null || eventEntity is null) return Error.Conflict("Talks.MissingReference", "Não é possível emitir certificado sem as referências de origem.");
            certificate.RecordSnapshot(owner.PersonName, talk.TalkTitle, eventEntity.EventName, talk.TalkStart);
            var write = await db.ExecuteInTransactionAsync(async ct =>
            {
                await db.SaveChangesAsync(ct);
                return Result.Success();
            }, cancellationToken);
            if (write.IsFailure)
            {
                logger.LogWarning("Persistência do certificado {CertificateId} falhou pela regra {ErrorCode}", certificate.Id, write.Error.Code);
                return write.Error;
            }
        }

        return new IssueCertificateResponse(
            certificate.Id,
            certificate.CertificateCode,
            talk.Id,
            certificate.TalkTitleSnapshot,
            certificate.PersonId,
            certificate.PersonNameSnapshot,
            certificate.CertificateIssuedAt,
            certificate.CertificateDurationMinutes)
        { Created = created };
    }
}
