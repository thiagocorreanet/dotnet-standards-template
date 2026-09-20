namespace Shared.Contracts.Common;

/// <summary>Usuário autenticado na requisição corrente. Fonte para auditoria (CreatedBy/UpdatedBy/DeletedBy).</summary>
public interface ICurrentUser
{
    Guid? Id { get; }
    string? Name { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool HasRole(string role);
}
