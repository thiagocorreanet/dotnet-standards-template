using FluentValidation.TestHelper;
using Module.Talks.Domain;
using Module.Talks.UseCases.AddContent;
using Module.Talks.UseCases.UpdateTalk;
using Module.Talks.UseCases.CreateTalk;
using Module.Talks.UseCases.ListTalks;
using Shouldly;

namespace Tests.Unit.Talks;

public class ValidatorsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static CreateTalkRequest CreateValid() => new(
        Guid.NewGuid(), Guid.NewGuid(), null, "Título", null, Start, Start.AddHours(1),
        [new CreateTalkSpeakerRequest(Guid.NewGuid(), SpeakerRole.Principal)]);

    [Fact]
    public void CreateTalk_valid_should_pass()
    {
        new CreateTalkValidator().TestValidate(CreateValid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateTalk_end_before_of_start_should_fail()
    {
        var request = CreateValid() with { TalkEnd = Start.AddMinutes(-1) };

        new CreateTalkValidator().TestValidate(request).ShouldHaveValidationErrorFor(r => r.TalkEnd);
    }

    [Fact]
    public void CreateTalk_without_speakers_should_fail()
    {
        var request = CreateValid() with { Speakers = [] };

        new CreateTalkValidator().TestValidate(request).ShouldHaveValidationErrorFor(r => r.Speakers);
    }

    [Fact]
    public void CreateTalk_with_person_empty_or_role_invalid_should_fail()
    {
        var request = CreateValid() with { Speakers = [new CreateTalkSpeakerRequest(Guid.Empty, (SpeakerRole)99)] };

        var result = new CreateTalkValidator().TestValidate(request);

        result.ShouldHaveValidationErrorFor("Speakers[0].PersonId");
        result.ShouldHaveValidationErrorFor("Speakers[0].SpeakerRole");
    }

    [Fact]
    public void CreateTalk_title_empty_event_empty_and_room_empty_should_fail()
    {
        var request = CreateValid() with { EventId = Guid.Empty, RoomId = Guid.Empty, TalkTitle = " ", TalkDescription = new string('x', 4001) };

        var result = new CreateTalkValidator().TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.EventId);
        result.ShouldHaveValidationErrorFor(r => r.RoomId);
        result.ShouldHaveValidationErrorFor(r => r.TalkTitle);
        result.ShouldHaveValidationErrorFor(r => r.TalkDescription);
    }

    [Fact]
    public void UpdateTalk_requires_id_of_route_and_period_valid()
    {
        var request = new UpdateTalkRequest(Guid.NewGuid(), null, "Título", null, Start, Start);

        var result = new UpdateTalkValidator().TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.TalkId);
        result.ShouldHaveValidationErrorFor(r => r.TalkEnd);
    }

    [Fact]
    public void AddContent_requires_url_absolute_and_type_valid()
    {
        var invalid = new AddContentRequest("Slides", (ContentType)42, "not-a-url", null) { TalkId = Guid.NewGuid() };
        var valid = new AddContentRequest("Slides", ContentType.Slides, "https://exemplo.com/a.pdf", null) { TalkId = Guid.NewGuid() };

        var result = new AddContentValidator().TestValidate(invalid);
        result.ShouldHaveValidationErrorFor(r => r.ContentUrl);
        result.ShouldHaveValidationErrorFor(r => r.ContentType);
        new AddContentValidator().TestValidate(valid).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ListTalks_limits_paging()
    {
        var result = new ListTalksValidator().TestValidate(new ListTalksRequest(null, null, 0, 101));

        result.ShouldHaveValidationErrorFor(r => r.Page);
        result.ShouldHaveValidationErrorFor(r => r.PageSize);
        result.Errors.Count.ShouldBe(2);
    }
}
