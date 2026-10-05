using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Endpoints;
using Shared.Http.Results;
#if (command)
using Shared.Http.Validation;
#endif

namespace Module.ModuleName.UseCases.UseCaseName;

internal sealed class UseCaseNameEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
#if (command)
        group.MapPost("/use-case-route", async (UseCaseNameRequest request, IUseCase<UseCaseNameRequest, UseCaseNameResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request, ct)).ToHttpResult())
            .WithName("UseCaseName")
            .WithSummary("Descreva a operação em uma frase")
            .WithDescription("""
                Descreva em pt-BR: campos, regras, erros (`Module.Reason`), eventos publicados e perfil exigido.
                """)
            .WithValidation<UseCaseNameRequest>()
            .RequireAuthorization()
            .Produces<UseCaseNameResponse>();
#else
        group.MapGet("/use-case-route/{id:guid}", async (Guid id, IUseCase<UseCaseNameRequest, UseCaseNameResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new UseCaseNameRequest(id), ct)).ToHttpResult())
            .WithName("UseCaseName")
            .WithSummary("Descreva a consulta em uma frase")
            .WithDescription("""
                Descreva em pt-BR: o que retorna, filtros, erros (`Module.Reason`) e perfil exigido.
                """)
            .RequireAuthorization()
            .Produces<UseCaseNameResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
#endif
}
