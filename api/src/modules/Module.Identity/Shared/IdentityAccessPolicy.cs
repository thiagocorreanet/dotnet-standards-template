using System.Reflection;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Module.Identity.UseCases.GetCurrentUser;
using Module.Identity.UseCases.RegisterUser;
using Module.Identity.UseCases.ListUsers;
using Module.Identity.UseCases.UpdateUserAccess;
namespace Module.Identity.Shared;
internal sealed class IdentityAccessPolicy(ICurrentUser user) : IModuleAccessPolicy
{
    public Assembly ModuleAssembly => typeof(IdentityModule).Assembly;
    public Task<bool> CanExecuteAsync(object request, CancellationToken ct) => Task.FromResult(user.IsAuthenticated && request switch
    {
        GetCurrentUserRequest r => r.UserId == user.Id,
        RegisterUserRequest or ListUsersRequest or UpdateUserAccessRequest => user.HasRole(DefaultRoles.Administrator),
        _ => false
    });
}
