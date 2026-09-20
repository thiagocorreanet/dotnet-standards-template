using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.Talks.UseCases.AddContent;

internal sealed class AddContentEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/{id:guid}/contents", async (Guid id, AddContentRequest request, IUseCase<AddContentRequest, AddContentResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request with { TalkId = id }, ct)).ToCreatedResult(r => $"/api/v1/talks/{r.TalkId}"))
            .WithName("AddContent")
            .WithSummary("Adiciona um conteúdo (material) à palestra")
            .WithDescription("""
                Registra um material da palestra apontando para uma URL absoluta (armazenamento externo).

                Tipos de conteúdo: `Slides`, `Pdf`, `File`, `Link`, `Video`, `Image`.

                **Perfil exigido:** Administrador ou Organizador.
                """)
            .WithValidation<AddContentRequest>()
            .RequireAuthorization(Policies.Management)
            .Produces<AddContentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
