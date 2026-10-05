using Module.People.Shared;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.People.UseCases.DeletePerson;

/// <summary>Administrador ou o titular vinculado à pessoa.</summary>
internal sealed class DeletePersonAccessPolicy(PersonOwnership ownership, ICurrentUser user) : IAccessPolicy<DeletePersonRequest>
{
    public async Task<bool> CanExecuteAsync(DeletePersonRequest request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.Id is null) return false;
        if (user.HasRole(DefaultRoles.Administrator)) return true;
        return await ownership.IsOwnerAsync(request.PersonId, ct);
    }
}
