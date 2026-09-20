using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;
using Shared.Data.Interceptors;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;
namespace Tests.Integration.Security;
[Collection(ApiCollection.Name)]
public sealed class AuditPrivacyTests(ApiFactory factory)
{
    [Fact]
    public async Task Audit_of_composite_and_delete_physical_not_exposes_values_by_default()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var options = new DbContextOptionsBuilder<AuditProbeContext>().UseNpgsql(factory.ConnectionString)
            .AddInterceptors(scope.ServiceProvider.GetRequiredService<AuditSaveChangesInterceptor>()).Options;
        await using var db = new AuditProbeContext(options);
        await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
        var row = new CompositeRow { Left = "part-a", Right = "part-b", Secret = "sensitive-canary@example.test" };
        db.Rows.Add(row);
        await db.SaveChangesAsync();
        row.Secret = "changed-secret@example.test";
        await db.SaveChangesAsync();
        db.Rows.Remove(row);
        await db.SaveChangesAsync();
        var messages = await db.OutboxMessages.OrderBy(m => m.OccurredOn).ToListAsync();
        messages.Count.ShouldBe(3);
        foreach (var message in messages)
        {
            message.Payload.ShouldNotContain("@example.test");
            using var json = JsonDocument.Parse(message.Payload);
            json.RootElement.GetProperty("entityId").GetString().ShouldBe("Left=part-a|Right=part-b");
        }
        messages.Select(m => JsonDocument.Parse(m.Payload).RootElement.GetProperty("operation").GetString())
            .ShouldContain("Delete");
    }
    private sealed class CompositeRow
    {
        public string Left { get; set; } = "";
        public string Right { get; set; } = "";
        public string Secret { get; set; } = "";
    }
    private sealed class AuditProbeContext(DbContextOptions<AuditProbeContext> options) : ModuleDbContext(options)
    {
        public override string Schema => "AuditProbe";
        public DbSet<CompositeRow> Rows => Set<CompositeRow>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompositeRow>().HasKey(r => new { r.Left, r.Right });
            base.OnModelCreating(modelBuilder);
        }
    }
}
