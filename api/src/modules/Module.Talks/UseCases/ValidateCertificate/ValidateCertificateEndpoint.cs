using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.ValidateCertificate;

internal sealed class ValidateCertificateEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/certificates/{code}", async (string code, IUseCase<ValidateCertificateRequest, ValidateCertificateResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new ValidateCertificateRequest(code), ct)).ToHttpResult())
            .WithName("ValidateCertificate")
            .WithSummary("Valida publicamente um certificado pelo código")
            .WithDescription("""
                Endpoint **público** (sem autenticação) para conferência de autenticidade de certificados.

                Informe o código de 12 caracteres impresso no certificado. Retorna título da palestra, evento, titular mascarado (exceto titular/Administrador),
                data da palestra, data de emissão e carga horária. Código desconhecido: `404 Talks.CertificateNotFound`.
                """)
            .AllowAnonymous()
            .Produces<ValidateCertificateResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
}
