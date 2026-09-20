using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.Talks.UseCases.GetTalk;

internal sealed class GetTalkEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/{id:guid}", async (Guid id, IUseCase<GetTalkRequest, GetTalkResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(new GetTalkRequest(id), ct)).ToHttpResult())
            .WithName("GetTalk")
            .WithSummary("Obtém uma palestra com palestrantes e conteúdos")
            .WithDescription("""
                Retorna a palestra com carga horária calculada (`talkEnd - talkStart`, em minutos), palestrantes, conteúdos e
                quantidades de presenças e certificados.

                Os campos `eventName`, `roomName` e `personName` são enriquecidos via contratos dos módulos Eventos, Locais e Pessoas.
                """)
            .Produces<GetTalkResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
}
