using Shared.Data.Entities;

namespace Module.Talks.Domain;

/// <summary>Vínculo entre uma palestra e uma pessoa (módulo Pessoas) com o papel exercido.</summary>
public sealed class TalkSpeaker : BaseEntity
{
    private TalkSpeaker()
    {
    }

    internal TalkSpeaker(Guid talkId, Guid personId, SpeakerRole speakerRole)
    {
        TalkId = talkId;
        PersonId = personId;
        SpeakerRole = speakerRole;
    }

    public Guid TalkId { get; private set; }
    public Guid PersonId { get; private set; }
    public SpeakerRole SpeakerRole { get; private set; }
}
