using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;

namespace Module.Audit.UseCases.ListAuditRecords;

/// <summary>Somente administrador consulta a trilha de auditoria.</summary>
internal sealed class ListAuditRecordsAccessPolicy(ICurrentUser user) : IAccessPolicy<ListAuditRecordsRequest>
{
    public Task<bool> CanExecuteAsync(ListAuditRecordsRequest request, CancellationToken ct) =>
        Task.FromResult(user.IsAuthenticated && user.HasRole(DefaultRoles.Administrator));
}
