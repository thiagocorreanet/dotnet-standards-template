using System.Linq.Expressions;

namespace Module.Talks.Domain;

/// <summary>Regras de agenda: sobreposição de horários entre palestras na mesma sala.</summary>
public static class TalkSchedule
{
    /// <summary>Dois períodos se sobrepõem quando um começa antes de o outro terminar e termina depois de o outro começar (limites tocantes não contam).</summary>
    public static bool Overlaps(DateTimeOffset start, DateTimeOffset end, DateTimeOffset otherStart, DateTimeOffset otherEnd) =>
        start < otherEnd && end > otherStart;

    /// <summary>Predicado traduzível pelo EF Core: palestras ativas que ocupam a sala no período, ignorando opcionalmente a própria palestra (atualização).</summary>
    public static Expression<Func<Talk, bool>> OccupiesRoom(Guid roomId, DateTimeOffset start, DateTimeOffset end, Guid? ignoredTalkId = null) =>
        p => p.RoomId == roomId
             && p.IsActive
             && p.TalkStart < end
             && p.TalkEnd > start
             && (ignoredTalkId == null || p.Id != ignoredTalkId);
}
