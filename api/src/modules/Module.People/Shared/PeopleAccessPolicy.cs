using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Module.People.UseCases.CreatePerson;
using Module.People.UseCases.GetPerson;
using Module.People.UseCases.UpdatePerson;
using Module.People.UseCases.DeletePerson;
namespace Module.People.Shared;
internal sealed class PeopleAccessPolicy(PeopleDbContext db, ICurrentUser user) : IModuleAccessPolicy
{
    public Assembly ModuleAssembly => typeof(PeopleModule).Assembly;
    public async Task<bool> CanExecuteAsync(object request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        if (request is CreatePersonRequest create)
            return (create.UserId is null || create.UserId == user.Id) && !await db.People.AnyAsync(p => p.UserId == user.Id, ct);
        var id = request switch { GetPersonRequest r => r.PersonId, UpdatePersonRequest r => r.PersonId, DeletePersonRequest r => r.PersonId, _ => Guid.Empty };
        return id != Guid.Empty && await db.People.AnyAsync(p => p.Id == id && p.UserId == user.Id, ct);
    }
}
