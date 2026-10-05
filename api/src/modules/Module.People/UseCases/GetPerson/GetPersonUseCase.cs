using Microsoft.EntityFrameworkCore;
using Module.People.Domain;
using Module.People.Shared;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;

namespace Module.People.UseCases.GetPerson;

internal sealed class GetPersonUseCase(PeopleDbContext db) : IUseCase<GetPersonRequest, GetPersonResponse>
{
    public async Task<Result<GetPersonResponse>> HandleAsync(GetPersonRequest request, CancellationToken cancellationToken)
    {
        var person = await db.People
            .TagWith("People.GetPerson")
            .AsNoTracking()
            .Where(p => p.Id == request.PersonId)
            .Select(p => new GetPersonResponse(
                p.Id, p.PersonName, p.PersonEmail, p.PersonPhone, p.PersonDocument, p.PersonCompany, p.PersonJobTitle,
                p.PersonShortBio, p.PersonPhotoUrl, p.IsActive, p.CreatedAt, p.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (person is null)
        {
            return PeopleErrors.PersonNotFound;
        }

        return person;
    }
}
