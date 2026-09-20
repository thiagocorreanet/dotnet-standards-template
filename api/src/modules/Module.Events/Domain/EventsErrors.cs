using Shared.Http.Results;

namespace Module.Events.Domain;

public static class EventsErrors
{
    public static readonly Error EventNotFound = Error.NotFound("Events.EventNotFound", "Evento não encontrado.");
    public static readonly Error VenueNotFound = Error.BusinessRule("Events.VenueNotFound", "O local informado não existe.");
    public static readonly Error InconsistentFormat = Error.BusinessRule("Events.InconsistentFormat", "Os dados de local e link remoto não são compatíveis com o formato do evento.");
    public static readonly Error EventWithoutTalks = Error.BusinessRule("Events.EventWithoutTalks", "O evento precisa ter ao menos uma palestra para ser publicado.");
    public static readonly Error CancellationReasonRequired = Error.BusinessRule("Events.CancellationReasonRequired", "Informe o motivo do cancelamento.");
    public static readonly Error InvalidStatusTransition = Error.BusinessRule("Events.InvalidStatusTransition", "Transição de situação não permitida para o evento.");
    public static readonly Error EventCannotBeUpdated = Error.BusinessRule("Events.EventCannotBeUpdated", "Somente eventos em rascunho ou publicados podem ser alterados.");
    public static readonly Error EventCannotBeDeleted = Error.BusinessRule("Events.EventCannotBeDeleted", "Somente eventos em rascunho ou cancelados podem ser excluídos.");
    public static readonly Error EventDoesNotAcceptRegistrations = Error.BusinessRule("Events.EventDoesNotAcceptRegistrations", "O evento não aceita inscrições na situação atual.");
    public static readonly Error PersonNotFound = Error.BusinessRule("Events.PersonNotFound", "A pessoa informada não existe.");
    public static readonly Error PersonAlreadyRegistered = Error.Conflict("Events.PersonAlreadyRegistered", "A pessoa já possui inscrição confirmada neste evento.");
    public static readonly Error CapacityExhausted = Error.BusinessRule("Events.CapacityExhausted", "A capacidade do evento foi atingida.");
    public static readonly Error RegistrationNotFound = Error.NotFound("Events.RegistrationNotFound", "Inscrição não encontrada neste evento.");
    public static readonly Error RegistrationAlreadyCanceled = Error.BusinessRule("Events.RegistrationAlreadyCanceled", "A inscrição já está cancelada.");
    public static readonly Error TrackNotFound = Error.NotFound("Events.TrackNotFound", "Trilha não encontrada neste evento.");
    public static readonly Error DuplicateTrackName = Error.Conflict("Events.DuplicateTrackName", "Já existe uma trilha com este nome no evento.");
    public static readonly Error EventRequiresTrack = Error.BusinessRule("Events.EventRequiresTrack", "O evento precisa manter ao menos uma trilha.");
}
