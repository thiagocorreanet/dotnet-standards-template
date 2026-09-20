namespace Shared.Contracts.Identity;

/// <summary>Perfis (roles) conhecidos por toda a aplicação.</summary>
public static class DefaultRoles
{
    public const string Administrator = "Administrator";
    public const string Organizer = "Organizer";
    public const string Participant = "Participant";

    public static readonly IReadOnlyList<string> All = [Administrator, Organizer, Participant];
}

/// <summary>Políticas de autorização compartilhadas entre os módulos.</summary>
public static class Policies
{
    /// <summary>Exige perfil Administrador.</summary>
    public const string Administration = "Administration";
    /// <summary>Exige perfil Administrador ou Organizador (gestão de eventos, palestras, locais, pessoas).</summary>
    public const string Management = "Management";
}
