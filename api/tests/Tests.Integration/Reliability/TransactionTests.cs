using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Module.Identity.Shared;
using Shared.Data.Transactions;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;
namespace Tests.Integration.Reliability;
[Collection(ApiCollection.Name)]
public sealed class TransactionTests(ApiFactory factory)
{
    [Theory]
    [InlineData("before-save")]
    [InlineData("before-commit")]
    [InlineData("after-commit")]
    public async Task Failure_before_of_persistence_or_in_commit_not_duplicates_business_nor_outbox(string stage)
    {
        var subject = "retry|" + Guid.NewGuid();
        if (stage == "after-commit") factory.Faults.ArmAfter();
        else if (stage == "before-save") factory.Faults.ArmBeforeSave();
        else factory.Faults.ArmBefore();
        using var client = factory.AuthenticatedClient();
        var result = await client.PostAsJsonAsync("/api/v1/identity/users", new
        {
            subject, userName = "Retry test", userEmail = "retry@example.test"
        });
        result.StatusCode.ShouldBe(HttpStatusCode.Created, await result.Content.ReadAsStringAsync());
        factory.Faults.BeforeCalls.ShouldBe(stage == "before-commit" ? 2 : 1);
        await factory.WithServiceAsync(async sp =>
        {
            var db = sp.GetRequiredService<IdentityDbContext>();
            var accounts = await db.Users.AsNoTracking().Where(u => u.Subject == subject).ToListAsync();
            accounts.Count.ShouldBe(1);
            var id = accounts[0].Id.ToString();
            var events = await db.OutboxMessages.Where(x => x.Type == "identity.user-registered.v1").Select(x => x.Payload).ToListAsync();
            events.Count(x => x.Contains(id, StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
            (await db.Set<CommandReceipt>().CountAsync()).ShouldBeGreaterThan(0);
            return true;
        });
    }
}
