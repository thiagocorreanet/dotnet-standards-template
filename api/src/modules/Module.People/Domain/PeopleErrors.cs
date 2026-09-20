using Shared.Http.Results;

namespace Module.People.Domain;

public static class PeopleErrors
{
    public static readonly Error PersonNotFound = Error.NotFound("People.PersonNotFound", "Pessoa não encontrada.");
    public static readonly Error EmailAlreadyRegistered = Error.Conflict("People.EmailAlreadyRegistered", "Já existe uma pessoa ativa com este e-mail.");
    public static readonly Error DocumentAlreadyRegistered = Error.Conflict("People.DocumentAlreadyRegistered", "Já existe uma pessoa ativa com este CPF.");
}
