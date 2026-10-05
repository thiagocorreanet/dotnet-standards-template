using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.Audit.UseCases.GetAuditRecord;

/// <summary>Somente administrador consulta a trilha de auditoria.</summary>
internal sealed class GetAuditRecordAccessPolicy(ICurrentUser user) : IAccessPolicy<GetAuditRecordRequest>
{
    public Task<bool> CanExecuteAsync(GetAuditRecordRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.HasRole(DefaultRoles.Administrator));
}
