using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.Talks.Domain;
using Module.Talks.Shared;
using Shared.Contracts.People;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.ListAttendances;

/// <summary>Lista as presenças da palestra com nome da pessoa (lote via contrato) e indicação de certificado emitido.</summary>
internal sealed class ListAttendancesUseCase(TalksDbContext db, IPeopleModuleApi peopleApi, ILogger<ListAttendancesUseCase> logger) : IUseCase<ListAttendancesRequest, IReadOnlyList<ListAttendancesItemResponse>>
{
    public async Task<Result<IReadOnlyList<ListAttendancesItemResponse>>> HandleAsync(ListAttendancesRequest request, CancellationToken cancellationToken)
    {
        var talkExists = await db.Talks
            .TagWith("Talks.ListAttendances.CheckTalk")
            .AnyAsync(p => p.Id == request.TalkId, cancellationToken);
        if (!talkExists)
        {
            logger.LogInformation("Listagem de presenças rejeitada: palestra {TalkId} não encontrada", request.TalkId);
            return TalksErrors.TalkNotFound;
        }

        var attendances = await db.Attendances
            .TagWith("Talks.ListAttendances")
            .AsNoTracking()
            .Where(p => p.TalkId == request.TalkId)
            .OrderBy(p => p.AttendanceRecordedAt)
            .Select(p => new
            {
                p.Id,
                p.PersonId,
                p.AttendanceRecordedAt,
                CertificateIssued = db.Certificates.Any(c => c.TalkId == p.TalkId && c.PersonId == p.PersonId),
            })
            .ToListAsync(cancellationToken);

        if (attendances.Count == 0)
        {
            logger.LogInformation("Palestra {TalkId} ainda não possui presenças registradas", request.TalkId);
            return Array.Empty<ListAttendancesItemResponse>();
        }

        var personIds = attendances.Select(p => p.PersonId).Distinct().ToList();
        logger.LogDebug("Consultando módulo Pessoas em lote para enriquecer {PersonCount} presença(s) distintas", personIds.Count);
        var people = await peopleApi.GetPeopleSummaryAsync(personIds, cancellationToken);
        var names = people.ToDictionary(p => p.Id, p => p.PersonName);
        logger.LogInformation("Palestra {TalkId} possui {AttendanceCount} presença(s), {CertificateCount} com certificado e {MissingPersonCount} pessoa(s) não localizada(s)",
            request.TalkId, attendances.Count, attendances.Count(p => p.CertificateIssued), personIds.Count(id => !names.ContainsKey(id)));

        logger.LogDebug("Mapeando {AttendanceCount} presença(s) com os resumos de pessoas", attendances.Count);
        var items = attendances
            .Select(p => new ListAttendancesItemResponse(p.Id, p.PersonId, names.GetValueOrDefault(p.PersonId, string.Empty), p.AttendanceRecordedAt, p.CertificateIssued))
            .ToList();
        return items;
    }
}
