using Module.Audit.UseCases.GetAuditRecord;
using Module.Audit.UseCases.ListAuditRecords;
using Shouldly;
using Tests.Unit.Shared;

namespace Tests.Unit.Audit;

public sealed class AccessPolicyTests
{
    public static TheoryData<string, bool> Users => new()
    {
        { "anonymous", false },
        { "participant", false },
        { "organizer", false },
        { "administrator", true },
    };

    [Theory]
    [MemberData(nameof(Users))]
    public async Task Only_administrator_reads_the_audit_trail(string profile, bool allowed)
    {
        var user = TestUsers.For(profile);
        (await new GetAuditRecordAccessPolicy(user).CanExecuteAsync(new GetAuditRecordRequest(Guid.NewGuid()), CancellationToken.None))
            .ShouldBe(allowed);
        (await new ListAuditRecordsAccessPolicy(user).CanExecuteAsync(new ListAuditRecordsRequest(null, null, null, null, null, null, null), CancellationToken.None))
            .ShouldBe(allowed);
    }
}
