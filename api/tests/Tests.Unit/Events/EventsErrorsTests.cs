using Module.Events.Domain;
using Shared.Http.Results;
using Shouldly;

namespace Tests.Unit.Events;

public sealed class EventsErrorsTests
{
    [Theory]
    [InlineData("EventNotFound", ErrorType.NotFound)]
    [InlineData("VenueNotFound", ErrorType.BusinessRule)]
    [InlineData("InconsistentFormat", ErrorType.BusinessRule)]
    [InlineData("EventWithoutTalks", ErrorType.BusinessRule)]
    [InlineData("CancellationReasonRequired", ErrorType.BusinessRule)]
    [InlineData("InvalidStatusTransition", ErrorType.BusinessRule)]
    [InlineData("EventCannotBeUpdated", ErrorType.BusinessRule)]
    [InlineData("EventCannotBeDeleted", ErrorType.BusinessRule)]
    [InlineData("EventDoesNotAcceptRegistrations", ErrorType.BusinessRule)]
    [InlineData("PersonNotFound", ErrorType.BusinessRule)]
    [InlineData("PersonAlreadyRegistered", ErrorType.Conflict)]
    [InlineData("CapacityExhausted", ErrorType.BusinessRule)]
    [InlineData("RegistrationNotFound", ErrorType.NotFound)]
    [InlineData("RegistrationAlreadyCanceled", ErrorType.BusinessRule)]
    public void Codes_and_types_should_follow_the_specification(string reason, ErrorType type)
    {
        var field = typeof(EventsErrors).GetField(reason);
        field.ShouldNotBeNull();
        var error = (Error)field.GetValue(null)!;

        error.Code.ShouldBe($"Events.{reason}");
        error.Type.ShouldBe(type);
    }
}
