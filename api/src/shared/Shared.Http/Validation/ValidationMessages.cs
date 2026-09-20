namespace Shared.Http.Validation;

/// <summary>Mensagens padrão em pt-BR reutilizadas pelos validators dos módulos.</summary>
public static class ValidationMessages
{
    public const string Required = "O campo {PropertyName} é obrigatório.";
    public const string MaximumLength = "O campo {PropertyName} deve ter no máximo {MaxLength} caracteres.";
    public const string EmailInvalid = "O campo {PropertyName} deve ser um e-mail válido.";
    public const string GreaterThanZero = "O campo {PropertyName} deve ser maior que zero.";
    public const string GuidRequired = "O campo {PropertyName} deve ser um identificador válido.";
}
