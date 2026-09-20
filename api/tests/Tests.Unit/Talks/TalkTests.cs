using Module.Talks.Domain;
using Shared.Contracts.Talks;
using Shouldly;

namespace Tests.Unit.Talks;

public class TalkTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddMinutes(90);

    private static Talk NewTalk(params Guid[] personIds)
    {
        var speakers = personIds.Select(id => new NewSpeaker(id, SpeakerRole.Principal)).ToList();
        var result = Talk.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  Monolito modular  ", null, Start, End, speakers);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    [Fact]
    public void Create_should_record_speakers_title_normalized_and_event_TalkCreated()
    {
        var person = Guid.NewGuid();
        var talk = NewTalk(person);

        talk.TalkTitle.ShouldBe("Monolito modular");
        talk.TalkDurationMinutes.ShouldBe(90);
        talk.Speakers.ShouldHaveSingleItem().PersonId.ShouldBe(person);
        talk.Events.ShouldHaveSingleItem().ShouldBeOfType<TalkCreated>().TalkId.ShouldBe(talk.Id);
    }

    [Fact]
    public void Create_without_speakers_should_fail()
    {
        var result = Talk.Create(Guid.NewGuid(), Guid.NewGuid(), null, "Título", null, Start, End, []);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TalksErrors.TalkRequiresSpeaker);
    }

    [Fact]
    public void Create_with_person_repeated_should_return_SpeakerAlreadyLinked()
    {
        var person = Guid.NewGuid();
        var result = Talk.Create(Guid.NewGuid(), Guid.NewGuid(), null, "Título", null, Start, End,
            [new NewSpeaker(person, SpeakerRole.Principal), new NewSpeaker(person, SpeakerRole.Coauthor)]);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TalksErrors.SpeakerAlreadyLinked);
    }

    [Fact]
    public void AddSpeaker_repeated_should_return_SpeakerAlreadyLinked()
    {
        var person = Guid.NewGuid();
        var talk = NewTalk(person);

        var result = talk.AddSpeaker(person, SpeakerRole.Moderator);

        result.Error.ShouldBe(TalksErrors.SpeakerAlreadyLinked);
    }

    [Fact]
    public void RemoveSpeaker_not_should_remove_the_last()
    {
        var person = Guid.NewGuid();
        var talk = NewTalk(person);

        var result = talk.RemoveSpeaker(person);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TalksErrors.TalkRequiresSpeaker);
    }

    [Fact]
    public void RemoveSpeaker_with_more_of_a_should_return_the_link()
    {
        var person1 = Guid.NewGuid();
        var person2 = Guid.NewGuid();
        var talk = NewTalk(person1, person2);

        var result = talk.RemoveSpeaker(person2);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PersonId.ShouldBe(person2);
    }

    [Fact]
    public void RemoveSpeaker_missing_should_return_SpeakerNotFound()
    {
        var talk = NewTalk(Guid.NewGuid(), Guid.NewGuid());

        talk.RemoveSpeaker(Guid.NewGuid()).Error.ShouldBe(TalksErrors.SpeakerNotFound);
    }

    [Fact]
    public void Contents_add_and_remove()
    {
        var talk = NewTalk(Guid.NewGuid());

        var content = talk.AddContent(" Slides ", ContentType.Slides, "https://exemplo.com/slides.pdf ", null);
        content.ContentTitle.ShouldBe("Slides");
        content.ContentUrl.ShouldBe("https://exemplo.com/slides.pdf");
        talk.Contents.ShouldHaveSingleItem();

        talk.RemoveContent(content.Id).IsSuccess.ShouldBeTrue();
        talk.RemoveContent(Guid.NewGuid()).Error.ShouldBe(TalksErrors.ContentNotFound);
    }

    [Fact]
    public void RecordAttendance_should_be_single_and_issue_AttendanceRecorded()
    {
        var talk = NewTalk(Guid.NewGuid());
        talk.ClearEvents();
        var participant = Guid.NewGuid();

        var first = talk.RecordAttendance(participant, Start.AddMinutes(5));
        var second = talk.RecordAttendance(participant, Start.AddMinutes(6));

        first.IsSuccess.ShouldBeTrue();
        first.Value.AttendanceRecordedAt.ShouldBe(Start.AddMinutes(5));
        second.Error.ShouldBe(TalksErrors.AttendanceAlreadyRecorded);
        talk.Attendances.ShouldHaveSingleItem();
        talk.Events.ShouldHaveSingleItem().ShouldBeOfType<AttendanceRecorded>().PersonId.ShouldBe(participant);
    }

    [Fact]
    public void IssueCertificate_without_attendance_should_return_AttendanceNotRecorded()
    {
        var talk = NewTalk(Guid.NewGuid());

        talk.IssueCertificate(Guid.NewGuid(), End.AddHours(1)).Error.ShouldBe(TalksErrors.AttendanceNotRecorded);
    }

    [Fact]
    public void IssueCertificate_before_of_end_should_return_TalkNotEnded()
    {
        var talk = NewTalk(Guid.NewGuid());
        var participant = Guid.NewGuid();
        talk.RecordAttendance(participant, Start).IsSuccess.ShouldBeTrue();

        talk.IssueCertificate(participant, End.AddMinutes(-1)).Error.ShouldBe(TalksErrors.TalkNotEnded);
        talk.Certificates.ShouldBeEmpty();
    }

    [Fact]
    public void IssueCertificate_after_the_end_should_create_with_code_and_duration_minutes_and_issue_event()
    {
        var talk = NewTalk(Guid.NewGuid());
        var participant = Guid.NewGuid();
        talk.RecordAttendance(participant, Start);
        talk.ClearEvents();

        var result = talk.IssueCertificate(participant, End);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeTrue();
        result.Value.Certificate.CertificateCode.Length.ShouldBe(CertificateCodeGenerator.Size);
        result.Value.Certificate.CertificateDurationMinutes.ShouldBe(90);
        result.Value.Certificate.CertificateIssuedAt.ShouldBe(End);
        talk.Events.ShouldHaveSingleItem().ShouldBeOfType<CertificateIssued>().CertificateId.ShouldBe(result.Value.Certificate.Id);
    }

    [Fact]
    public void IssueCertificate_should_be_idempotent()
    {
        var talk = NewTalk(Guid.NewGuid());
        var participant = Guid.NewGuid();
        talk.RecordAttendance(participant, Start);

        var first = talk.IssueCertificate(participant, End.AddHours(1));
        talk.ClearEvents();
        var second = talk.IssueCertificate(participant, End.AddHours(2));

        first.Value.Created.ShouldBeTrue();
        second.Value.Created.ShouldBeFalse();
        second.Value.Certificate.ShouldBeSameAs(first.Value.Certificate);
        talk.Certificates.ShouldHaveSingleItem();
        talk.Events.ShouldBeEmpty();
    }
}
