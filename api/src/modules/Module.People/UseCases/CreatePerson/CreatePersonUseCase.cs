using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.People.Domain;
using Module.People.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.People.UseCases.CreatePerson;

[Command("event-management-example")]
internal sealed class CreatePersonUseCase(PeopleDbContext db, ICurrentUser user, ILogger<CreatePersonUseCase> logger) : IUseCase<CreatePersonRequest, CreatePersonResponse>
{
    public async Task<Result<CreatePersonResponse>> HandleAsync(CreatePersonRequest request, CancellationToken cancellationToken)
    {
        logger.LogDebug("Normalizando e verificando unicidade do e-mail da nova pessoa");
        var email = Person.NormalizeEmail(request.PersonEmail);
        var emailInUse = await db.People
            .TagWith("People.CreatePerson.CheckEmail")
            .AnyAsync(p => p.PersonEmail == email, cancellationToken);
        if (emailInUse)
        {
            logger.LogInformation("Criação de pessoa rejeitada porque o e-mail normalizado já está cadastrado");
            return PeopleErrors.EmailAlreadyRegistered;
        }

        var document = Cpf.Normalize(request.PersonDocument);
        logger.LogDebug("Documento informado para nova pessoa={HasDocument}", document is not null);
        if (document is not null)
        {
            var documentInUse = await db.People
                .TagWith("People.CreatePerson.CheckDocument")
                .AnyAsync(p => p.PersonDocument == document, cancellationToken);
            if (documentInUse)
            {
                logger.LogInformation("Criação de pessoa rejeitada porque o documento normalizado já está cadastrado");
                return PeopleErrors.DocumentAlreadyRegistered;
            }
        }

        logger.LogDebug("Chamando fábrica de domínio Pessoa.Criar");
        var person = Person.Create(
            request.PersonName, request.PersonEmail, request.PersonPhone, request.PersonDocument,
            request.PersonCompany, request.PersonJobTitle, request.PersonShortBio, request.PersonPhotoUrl);

        person.LinkUser(user.HasRole(DefaultRoles.Administrator) ? request.UserId : user.Id);
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.People.Add(person);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Pessoa {PersonId} criada", person.Id);
            return Result.Success(new CreatePersonResponse(person.Id, person.PersonName, person.PersonEmail));
        }, cancellationToken);
    }
}
