using FluentValidation;
using Shared.Http.Validation;
namespace Module.Events.UseCases.ListTracks;
internal sealed class ListTracksValidator : AbstractValidator<ListTracksRequest> { public ListTracksValidator() => RuleFor(x => x.EventId).NotEmpty().WithMessage(ValidationMessages.GuidRequired); }
