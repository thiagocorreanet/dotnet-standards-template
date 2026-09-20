using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.People.UseCases.GetPerson;

internal sealed class GetPersonEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/{id:guid}", async (Guid id, IUseCase<GetPersonRequest, GetPersonResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new GetPersonRequest(id), ct)).ToHttpResult())
            .WithName("GetPerson")
            .WithSummary("Obtém uma pessoa")
            .WithDescription("Retorna os dados cadastrais completos da pessoa (CPF sem máscara). Pessoas excluídas retornam `404 People.PersonNotFound`. **Perfil exigido:** qualquer usuário autenticado.")
            .Produces<GetPersonResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
}
