using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Module.People.Domain;
using Module.People.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.People.UseCases.CreatePerson;

[Command("event-management-example")]
internal sealed class CreatePersonUseCase(PeopleDbContext db, ICurrentUser user) : IUseCase<CreatePersonRequest, CreatePersonResponse>
{
    public async Task<Result<CreatePersonResponse>> HandleAsync(CreatePersonRequest request, CancellationToken cancellationToken)
    {
        var email = Person.NormalizeEmail(request.PersonEmail);
        var emailInUse = await db.People
            .TagWith("People.CreatePerson.CheckEmail")
            .AnyAsync(p => p.PersonEmail == email, cancellationToken);
        if (emailInUse)
        {
            return PeopleErrors.EmailAlreadyRegistered;
        }

        var document = Cpf.Normalize(request.PersonDocument);
        if (document is not null)
        {
            var documentInUse = await db.People
                .TagWith("People.CreatePerson.CheckDocument")
                .AnyAsync(p => p.PersonDocument == document, cancellationToken);
            if (documentInUse)
            {
                return PeopleErrors.DocumentAlreadyRegistered;
            }
        }

        var person = Person.Create(
            request.PersonName, request.PersonEmail, request.PersonPhone, request.PersonDocument,
            request.PersonCompany, request.PersonJobTitle, request.PersonShortBio, request.PersonPhotoUrl);

        person.LinkUser(user.HasRole(DefaultRoles.Administrator) ? request.UserId : user.Id);
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            db.People.Add(person);
            await db.SaveChangesAsync(ct);
            return Result.Success(new CreatePersonResponse(person.Id, person.PersonName, person.PersonEmail));
        }, cancellationToken);
    }
}
