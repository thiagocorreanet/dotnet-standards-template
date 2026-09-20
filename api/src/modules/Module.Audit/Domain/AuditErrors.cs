using Shared.Http.Results;

namespace Module.Audit.Domain;

public static class AuditErrors
{
    public static readonly Error RecordNotFound = Error.NotFound("Audit.RecordNotFound", "Registro de auditoria não encontrado.");
}
