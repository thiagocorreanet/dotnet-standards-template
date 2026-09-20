using System.Security.Cryptography;

namespace Module.Talks.Domain;

/// <summary>
/// Gera códigos de certificado com 12 caracteres de um alfabeto sem símbolos ambíguos (sem 0/O, 1/I/L),
/// usando gerador criptográfico. O alfabeto possui 31 símbolos; <see cref="RandomNumberGenerator.GetInt32(int)"/>
/// faz amostragem sem viés mesmo quando a quantidade não é potência de dois.
/// </summary>
public static class CertificateCodeGenerator
{
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int Size = 12;

    public static string Generate()
    {
        Span<char> code = stackalloc char[Size];
        for (var i = 0; i < Size; i++)
        {
            code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }
}
