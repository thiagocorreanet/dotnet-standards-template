using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Module.People.Domain;
using Module.People.Shared;
using Shared.Http.Endpoints;
using Shared.Http.Results;

namespace Module.People.UseCases.GetPerson;

internal sealed class GetPersonUseCase(PeopleDbContext db, ILogger<GetPersonUseCase> logger) : IUseCase<GetPersonRequest, GetPersonResponse>
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
            logger.LogInformation("Pessoa {PersonId} não encontrada para detalhamento", request.PersonId);
            return PeopleErrors.PersonNotFound;
        }

        logger.LogInformation("Pessoa {PersonId} carregada; ativo={Active}, documento informado={HasDocument}", person.Id, person.IsActive, person.PersonDocument is not null);
        return person;
    }
}
