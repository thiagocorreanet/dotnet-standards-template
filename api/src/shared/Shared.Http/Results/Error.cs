namespace Shared.Http.Results;

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    BusinessRule,
    Unauthorized,
    Forbidden,
}

/// <summary>
/// Erro de domínio/aplicação. <c>Code</c> segue "Module.Reason" (ex.: "Venues.VenueNotFound") e vira o
/// campo <c>code</c> do ProblemDetails, estável para o front e para dashboards.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error BusinessRule(string code, string message) => new(code, message, ErrorType.BusinessRule);
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}
