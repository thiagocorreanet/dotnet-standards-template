using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.People.Domain;
using Module.People.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.People.UseCases.UpdatePerson;

[Command("event-management-example")]
internal sealed class UpdatePersonUseCase(PeopleDbContext db, ILogger<UpdatePersonUseCase> logger) : IUseCase<UpdatePersonRequest, UpdatePersonResponse>
{
    public async Task<Result<UpdatePersonResponse>> HandleAsync(UpdatePersonRequest request, CancellationToken cancellationToken)
    {
        var person = await db.People
            .TagWith("People.UpdatePerson.Load")
            .FirstOrDefaultAsync(p => p.Id == request.PersonId, cancellationToken);
        if (person is null)
        {
            logger.LogInformation("Pessoa {PersonId} não encontrada para atualização", request.PersonId);
            return PeopleErrors.PersonNotFound;
        }

        logger.LogDebug("Normalizando e verificando unicidade do e-mail da pessoa {PersonId}", person.Id);
        var email = Person.NormalizeEmail(request.PersonEmail);
        var emailInUse = await db.People
            .TagWith("People.UpdatePerson.CheckEmail")
            .AnyAsync(p => p.Id != request.PersonId && p.PersonEmail == email, cancellationToken);
        if (emailInUse)
        {
            logger.LogInformation("Atualização da pessoa {PersonId} rejeitada porque o e-mail normalizado já está em uso", person.Id);
            return PeopleErrors.EmailAlreadyRegistered;
        }

        var document = Cpf.Normalize(request.PersonDocument);
        logger.LogDebug("Documento informado para pessoa {PersonId}={HasDocument}", person.Id, document is not null);
        if (document is not null)
        {
            var documentInUse = await db.People
                .TagWith("People.UpdatePerson.CheckDocument")
                .AnyAsync(p => p.Id != request.PersonId && p.PersonDocument == document, cancellationToken);
            if (documentInUse)
            {
                logger.LogInformation("Atualização da pessoa {PersonId} rejeitada porque o documento normalizado já está em uso", person.Id);
                return PeopleErrors.DocumentAlreadyRegistered;
            }
        }

        logger.LogDebug("Chamando agregado Pessoa {PersonId} para atualizar cadastro e ativo={Active}", person.Id, request.IsActive);
        person.Update(
            request.PersonName, request.PersonEmail, request.PersonPhone, request.PersonDocument,
            request.PersonCompany, request.PersonJobTitle, request.PersonShortBio, request.PersonPhotoUrl, request.IsActive);

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Pessoa {PersonId} atualizada; ativo={Active}", person.Id, person.IsActive);
            return Result.Success(new UpdatePersonResponse(person.Id, person.PersonName, person.PersonEmail, person.IsActive, person.UpdatedAt));
        }, cancellationToken);
    }
}
