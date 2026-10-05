using Microsoft.EntityFrameworkCore;
using Module.People.Domain;
using Module.People.Shared;
using Shared.Data.Extensions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.People.UseCases.UpdatePerson;

[Command("event-management-example")]
internal sealed class UpdatePersonUseCase(PeopleDbContext db) : IUseCase<UpdatePersonRequest, UpdatePersonResponse>
{
    public async Task<Result<UpdatePersonResponse>> HandleAsync(UpdatePersonRequest request, CancellationToken cancellationToken)
    {
        var person = await db.People
            .TagWith("People.UpdatePerson.Load")
            .FirstOrDefaultAsync(p => p.Id == request.PersonId, cancellationToken);
        if (person is null)
        {
            return PeopleErrors.PersonNotFound;
        }

        var email = Person.NormalizeEmail(request.PersonEmail);
        var emailInUse = await db.People
            .TagWith("People.UpdatePerson.CheckEmail")
            .AnyAsync(p => p.Id != request.PersonId && p.PersonEmail == email, cancellationToken);
        if (emailInUse)
        {
            return PeopleErrors.EmailAlreadyRegistered;
        }

        var document = Cpf.Normalize(request.PersonDocument);
        if (document is not null)
        {
            var documentInUse = await db.People
                .TagWith("People.UpdatePerson.CheckDocument")
                .AnyAsync(p => p.Id != request.PersonId && p.PersonDocument == document, cancellationToken);
            if (documentInUse)
            {
                return PeopleErrors.DocumentAlreadyRegistered;
            }
        }

        person.Update(
            request.PersonName, request.PersonEmail, request.PersonPhone, request.PersonDocument,
            request.PersonCompany, request.PersonJobTitle, request.PersonShortBio, request.PersonPhotoUrl, request.IsActive);

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            await db.SaveChangesAsync(ct);
            return Result.Success(new UpdatePersonResponse(person.Id, person.PersonName, person.PersonEmail, person.IsActive, person.UpdatedAt));
        }, cancellationToken);
    }
}
