using Module.Audit.Domain;
using Shared.Contracts.Audit;
using Shouldly;

namespace Tests.Unit.Audit;

public sealed class AuditRecordTests
{
    [Fact]
    public void Create_should_copy_all_the_fields_of_event_and_use_the_id_of_event_as_key()
    {
        var userId = Guid.NewGuid();
        var eventEntity = new EntityChanged("People", "Person", "abc", AuditOperations.Update, "{\"PersonName\":\"A\"}", "{\"PersonName\":\"B\"}", userId, "Agente", "trace-1");
        var recordedAt = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

        var record = AuditRecord.Create(eventEntity, recordedAt);

        record.Id.ShouldBe(eventEntity.Id);
        record.Module.ShouldBe("People");
        record.EntityName.ShouldBe("Person");
        record.EntityId.ShouldBe("abc");
        record.Operation.ShouldBe(AuditOperations.Update);
        record.PreviousData.ShouldBe("{\"PersonName\":\"A\"}");
        record.NewData.ShouldBe("{\"PersonName\":\"B\"}");
        record.UserId.ShouldBe(userId);
        record.UserName.ShouldBe("Agente");
        record.TraceId.ShouldBe("trace-1");
        record.OccurredOn.ShouldBe(eventEntity.OccurredOn);
        record.RecordedAt.ShouldBe(recordedAt);
    }

    [Fact]
    public void Create_to_insertion_should_preserve_data_previous_null()
    {
        var eventEntity = new EntityChanged("Venues", "Venue", "1", AuditOperations.Insert, null, "{}", null, "sistema", null);

        var record = AuditRecord.Create(eventEntity, DateTimeOffset.UtcNow);

        record.PreviousData.ShouldBeNull();
        record.UserId.ShouldBeNull();
        record.TraceId.ShouldBeNull();
    }
}
