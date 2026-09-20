namespace Module.People.Domain;

/// <summary>
/// Regras do CPF (Cadastro de Pessoas Físicas): normalização (somente dígitos) e validação dos dígitos verificadores.
/// O documento é armazenado sempre normalizado; a máscara é responsabilidade da apresentação.
/// </summary>
public static class Cpf
{
    public const int Size = 11;

    /// <summary>Remove máscara e espaços, mantendo apenas dígitos. Retorna <c>null</c> quando não restar nenhum dígito.</summary>
    public static string? Normalize(string? document)
    {
        if (string.IsNullOrWhiteSpace(document))
        {
            return null;
        }

        var digits = new string(document.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    /// <summary>Valida tamanho, sequência repetida (ex.: 111.111.111-11) e os dois dígitos verificadores (módulo 11).</summary>
    public static bool IsValid(string? document)
    {
        var digits = Normalize(document);
        if (digits is null || digits.Length != Size || digits.Distinct().Count() == 1)
        {
            return false;
        }

        return CalculateDigit(digits, 9) == digits[9] - '0' && CalculateDigit(digits, 10) == digits[10] - '0';
    }

    private static int CalculateDigit(string digits, int count)
    {
        var sum = 0;
        var weight = count + 1;
        for (var i = 0; i < count; i++)
        {
            sum += (digits[i] - '0') * weight--;
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
