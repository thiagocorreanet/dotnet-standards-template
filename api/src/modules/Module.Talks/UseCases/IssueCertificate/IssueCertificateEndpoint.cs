using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.IssueCertificate;

internal sealed class IssueCertificateEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/{id:guid}/certificates", async (Guid id, IssueCertificateRequest request, IUseCase<IssueCertificateRequest, IssueCertificateResponse> useCase, CancellationToken ct) =>
            {
                var result = await useCase.HandleAsync(request with { TalkId = id }, ct);
                return result.IsSuccess && !result.Value.Created
                    ? result.ToHttpResult()
                    : result.ToCreatedResult(r => $"/api/v1/talks/certificates/{r.CertificateCode}");
            })
            .WithName("IssueCertificate")
            .WithSummary("Emite o certificado de participação de uma pessoa")
            .WithDescription("""
                Emite o certificado da pessoa na palestra e devolve o **código de validação** (12 caracteres, sem símbolos ambíguos).

                - Exige presença registrada: `422 Talks.AttendanceNotRecorded`.
                - Exige palestra encerrada (`talkEnd` já passou): `422 Talks.TalkNotEnded`.
                - **Idempotente:** se o certificado já existir, responde `200` com o existente; caso contrário `201` e emite `CertificateIssued`.

                A carga horária registrada é a duração da palestra em minutos. Exige titular, organizador proprietário ou Administrador.
                """)
            .WithValidation<IssueCertificateRequest>()
            .Produces<IssueCertificateResponse>(StatusCodes.Status201Created)
            .Produces<IssueCertificateResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
