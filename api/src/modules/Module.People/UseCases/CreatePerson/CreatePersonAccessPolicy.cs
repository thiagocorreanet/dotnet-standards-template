using Microsoft.EntityFrameworkCore;
using Module.People.Shared;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.People.UseCases.CreatePerson;

/// <summary>Administrador cadastra qualquer pessoa; os demais, apenas a própria e uma única vez.</summary>
internal sealed class CreatePersonAccessPolicy(PeopleDbContext db, ICurrentUser user) : IAccessPolicy<CreatePersonRequest>
{
    public async Task<bool> CanExecuteAsync(CreatePersonRequest request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        return (request.UserId is null || request.UserId == user.Id)
            && !await db.People.TagWith("People.CreatePerson.Access").AnyAsync(p => p.UserId == user.Id, ct);
    }
}
