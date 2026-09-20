using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Endpoints;
using Shared.Http.Results;
using Shared.Http.Validation;

namespace Module.People.UseCases.CreatePerson;

internal sealed class CreatePersonEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("", async (CreatePersonRequest request, IUseCase<CreatePersonRequest, CreatePersonResponse> useCase, CancellationToken ct) =>
                (await useCase.HandleAsync(request, ct)).ToCreatedResult(r => $"/api/v1/people/{r.Id}"))
            .WithName("CreatePerson")
            .WithSummary("Cadastra uma pessoa")
            .WithDescription("""
                Cadastra uma pessoa (palestrante ou participante; o papel nasce do relacionamento com eventos e palestras).

                | Campo | Regra |
                |-------|-------|
                | `personName` | Obrigatório, até 150 caracteres |
                | `personEmail` | Obrigatório, e-mail válido; armazenado em minúsculas; único entre pessoas ativas |
                | `personDocument` | Opcional; CPF com ou sem máscara, validado pelos dígitos verificadores; armazenado só com dígitos; único entre pessoas ativas |
                | `personPhone`, `personCompany`, `personJobTitle` | Opcionais (20/150/100 caracteres) |
                | `personShortBio` | Opcional, até 2000 caracteres |
                | `personPhotoUrl` | Opcional, URL absoluta http(s), até 500 caracteres |

                **Erros:** `400 Validation` · `409 People.EmailAlreadyRegistered` · `409 People.DocumentAlreadyRegistered`.

                Publica o evento de integração `PersonCreated`. **Perfil exigido:** Administrador ou Organizador.
                """)
            .WithValidation<CreatePersonRequest>()
            .RequireAuthorization()
            .Produces<CreatePersonResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
