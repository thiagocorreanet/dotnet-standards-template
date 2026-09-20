using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.RecordAttendance;

internal sealed class RecordAttendanceEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/{id:guid}/attendances", async (Guid id, RecordAttendanceRequest request, IUseCase<RecordAttendanceRequest, RecordAttendanceResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request with { TalkId = id }, ct)).ToCreatedResult(r => $"/api/v1/talks/{r.TalkId}/attendances"))
            .WithName("RecordAttendance")
            .WithSummary("Registra a presença de um participante na palestra")
            .WithDescription("""
                Registra a presença de uma pessoa na palestra (check-in).

                - A pessoa precisa ter **inscrição confirmada** no evento da palestra (módulo Eventos): `422 Talks.ParticipantNotRegistered`.
                - A presença é única por pessoa e palestra: `409 Talks.AttendanceAlreadyRecorded`.

                Emite o evento de integração `AttendanceRecorded`. **Perfil exigido:** Administrador ou Organizador.
                """)
            .WithValidation<RecordAttendanceRequest>()
            .RequireAuthorization(Policies.Management)
            .Produces<RecordAttendanceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
