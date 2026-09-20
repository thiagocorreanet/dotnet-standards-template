using FluentValidation;
using Module.Events.Domain;
using Shared.Http.Validation;

namespace Module.Events.UseCases.CreateEvent;

internal sealed class CreateEventValidator : AbstractValidator<CreateEventRequest>
{
    public CreateEventValidator()
    {
        RuleFor(r => r.EventName).NotEmpty().WithMessage(ValidationMessages.Required).MaximumLength(200).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.EventDescription).MaximumLength(4000).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.EventStartDate).NotEmpty().WithMessage(ValidationMessages.Required);
        RuleFor(r => r.EventEndDate).NotEmpty().WithMessage(ValidationMessages.Required)
            .GreaterThan(r => r.EventStartDate).WithMessage(EventValidationMessages.EndDateAfterStart);
        RuleFor(r => r.EventFormat).IsInEnum().WithMessage(EventValidationMessages.InvalidFormat);
        RuleFor(r => r.EventRemoteUrl).MaximumLength(500).WithMessage(ValidationMessages.MaximumLength);
        RuleFor(r => r.EventMaximumCapacity).GreaterThan(0).When(r => r.EventMaximumCapacity.HasValue).WithMessage(ValidationMessages.GreaterThanZero);
        RuleFor(r => r.VenueId).NotEqual(Guid.Empty).When(r => r.VenueId.HasValue).WithMessage(ValidationMessages.GuidRequired);

        RuleFor(r => r.VenueId).NotNull().WithMessage(EventValidationMessages.VenueRequired)
            .When(r => r.EventFormat is EventFormat.InPerson or EventFormat.Hybrid);
        RuleFor(r => r.VenueId).Null().WithMessage(EventValidationMessages.VenueNotAllowed)
            .When(r => r.EventFormat == EventFormat.Remote);
        RuleFor(r => r.EventRemoteUrl).NotEmpty().WithMessage(EventValidationMessages.LinkRequired)
            .When(r => r.EventFormat is EventFormat.Remote or EventFormat.Hybrid);
        RuleFor(r => r.Tracks).Must(x => x is null || x.Count > 0).WithMessage("Informe ao menos uma trilha.");
        RuleForEach(r => r.Tracks).ChildRules(t =>
        {
            t.RuleFor(x => x.TrackName).NotEmpty().WithMessage(ValidationMessages.Required).MaximumLength(120).WithMessage(ValidationMessages.MaximumLength);
            t.RuleFor(x => x.TrackDescription).MaximumLength(1000).WithMessage(ValidationMessages.MaximumLength);
            t.RuleFor(x => x.TrackColor).Matches("^#[0-9A-Fa-f]{6}$").When(x => !string.IsNullOrWhiteSpace(x.TrackColor)).WithMessage("Informe uma cor hexadecimal no formato #RRGGBB.");
        });
        RuleFor(r => r.Tracks).Must(x => x is null || x.Select(t => t.TrackName.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == x.Count).WithMessage("Os nomes das trilhas não podem se repetir.");
    }
}

/// <summary>Mensagens específicas do módulo Eventos (as genéricas ficam em <see cref="ValidationMessages"/>).</summary>
internal static class EventValidationMessages
{
    public const string EndDateAfterStart = "O campo {PropertyName} deve ser posterior à data de início.";
    public const string InvalidFormat = "O campo {PropertyName} deve ser Presencial, Remoto ou Hibrido.";
    public const string VenueRequired = "O campo {PropertyName} é obrigatório para eventos presenciais ou híbridos.";
    public const string VenueNotAllowed = "O campo {PropertyName} não deve ser informado para eventos remotos.";
    public const string LinkRequired = "O campo {PropertyName} é obrigatório para eventos remotos ou híbridos.";
    public const string InvalidStatus = "O campo {PropertyName} deve ser Publicado, EmAndamento, Encerrado ou Cancelado.";
}
