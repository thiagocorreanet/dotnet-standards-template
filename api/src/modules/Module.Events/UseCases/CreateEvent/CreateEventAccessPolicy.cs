using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.Events.UseCases.CreateEvent;

/// <summary>Administrador ou organizador cria eventos.</summary>
internal sealed class CreateEventAccessPolicy(ICurrentUser user) : IAccessPolicy<CreateEventRequest>
{
    public Task<bool> CanExecuteAsync(CreateEventRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.Id is not null
            && (user.HasRole(DefaultRoles.Administrator) || user.HasRole(DefaultRoles.Organizer)));
}
