using System.Reflection;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
namespace Module.Audit.Shared;
internal sealed class AuditAccessPolicy(ICurrentUser user) : IModuleAccessPolicy
{
    public Assembly ModuleAssembly => typeof(AuditModule).Assembly;
    public Task<bool> CanExecuteAsync(object request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.HasRole(DefaultRoles.Administrator));
}
