using Module.People.Shared;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.People.UseCases.GetPerson;

/// <summary>Administrador ou o titular vinculado à pessoa.</summary>
internal sealed class GetPersonAccessPolicy(PersonOwnership ownership, ICurrentUser user) : IAccessPolicy<GetPersonRequest>
{
    public async Task<bool> CanExecuteAsync(GetPersonRequest request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        return await ownership.IsOwnerAsync(request.PersonId, ct);
    }
}
