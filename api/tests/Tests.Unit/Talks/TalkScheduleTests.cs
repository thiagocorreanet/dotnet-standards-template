using Module.Talks.Domain;
using Shouldly;

namespace Tests.Unit.Talks;

public class TalkScheduleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    public static TheoryData<int, int, int, int, bool> Cases => new()
    {
        // (inicio, fim, outroInicio, outroFim) em minutos a partir de T0 → sobrepõe?
        { 0, 60, 30, 90, true },      // parcial no fim
        { 30, 90, 0, 60, true },      // parcial no início
        { 0, 120, 30, 60, true },     // contém
        { 30, 60, 0, 120, true },     // contido
        { 0, 60, 0, 60, true },       // idêntico
        { 0, 60, 60, 120, false },    // encosta no fim (limite não conta)
        { 60, 120, 0, 60, false },    // encosta no início
        { 0, 60, 90, 120, false },    // disjunto depois
        { 90, 120, 0, 60, false },    // disjunto antes
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Overlaps_should_detect_overlap_of_periods(int start, int end, int otherStart, int otherEnd, bool expected)
    {
        TalkSchedule.Overlaps(T0.AddMinutes(start), T0.AddMinutes(end), T0.AddMinutes(otherStart), T0.AddMinutes(otherEnd)).ShouldBe(expected);
    }

    [Fact]
    public void OccupiesRoom_should_consider_room_period_and_ignore_the_own_talk()
    {
        var room = Guid.NewGuid();
        var existing = Talk.Create(Guid.NewGuid(), Guid.NewGuid(), room, "Existing", null, T0, T0.AddMinutes(60), [new NewSpeaker(Guid.NewGuid(), SpeakerRole.Principal)]).Value;

        TalkSchedule.OccupiesRoom(room, T0.AddMinutes(30), T0.AddMinutes(90)).Compile()(existing).ShouldBeTrue();
        TalkSchedule.OccupiesRoom(room, T0.AddMinutes(60), T0.AddMinutes(90)).Compile()(existing).ShouldBeFalse();
        TalkSchedule.OccupiesRoom(Guid.NewGuid(), T0.AddMinutes(30), T0.AddMinutes(90)).Compile()(existing).ShouldBeFalse();
        TalkSchedule.OccupiesRoom(room, T0.AddMinutes(30), T0.AddMinutes(90), ignoredTalkId: existing.Id).Compile()(existing).ShouldBeFalse();
    }
}
