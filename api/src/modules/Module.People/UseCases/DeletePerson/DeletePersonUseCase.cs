using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.People.Domain;
using Module.People.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.People.UseCases.DeletePerson;

/// <summary>Exclusão lógica da pessoa (interceptor converte Remove em soft delete) com publicação de <c>PersonDeleted</c>.</summary>
[Command("event-management-example")]
internal sealed class DeletePersonUseCase(PeopleDbContext db, ILogger<DeletePersonUseCase> logger) : IUseCase<DeletePersonRequest, DeletePersonResponse>
{
    public async Task<Result<DeletePersonResponse>> HandleAsync(DeletePersonRequest request, CancellationToken cancellationToken)
    {
        var person = await db.People
            .TagWith("People.DeletePerson.Load")
            .FirstOrDefaultAsync(p => p.Id == request.PersonId, cancellationToken);
        if (person is null)
        {
            logger.LogInformation("Pessoa {PersonId} não encontrada para exclusão", request.PersonId);
            return PeopleErrors.PersonNotFound;
        }

        logger.LogDebug("Chamando agregado Pessoa {PersonId} para marcar exclusão e registrar evento de integração", person.Id);
        person.MarkDeleted();
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.People.Remove(person);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Pessoa {PersonId} excluída logicamente", person.Id);
            return Result.Success(new DeletePersonResponse(person.Id));
        }, cancellationToken);
    }
}
