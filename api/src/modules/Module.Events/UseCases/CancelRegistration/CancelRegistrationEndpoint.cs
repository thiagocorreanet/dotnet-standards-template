using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Events.UseCases.CancelRegistration;

internal sealed class CancelRegistrationEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapDelete("/{id:guid}/registrations/{registrationId:guid}", async (Guid id, Guid registrationId, IUseCase<CancelRegistrationRequest, CancelRegistrationResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new CancelRegistrationRequest(id, registrationId), ct)).ToNoContentResult())
            .WithName("CancelRegistration")
            .WithSummary("Cancela uma inscrição")
            .WithDescription("""
                Muda a situação da inscrição para **Cancelada** (não é exclusão lógica) e emite `RegistrationCanceled`.

                - Inscrição inexistente no evento: `404 Events.RegistrationNotFound`.
                - Inscrição já cancelada: `422 Events.RegistrationAlreadyCanceled`.

                **Perfil exigido:** qualquer usuário autenticado.
                """)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
