using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Module.Identity.Shared;
using Shared.Contracts.Audit;
using Shared.Contracts.Integration;
using Shared.Data.Migrations;
using Shared.Data.Outbox;
using Shared.Messaging;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;
namespace Tests.Integration.Reliability;
[Collection(ApiCollection.Name)]
public sealed class DeliveryContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Missing_required_audit_consumer_is_not_reported_as_delivery_success()
    {
        await using var app = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Outbox:Enabled", "false").ConfigureServices(s => s.RemoveAll<IIntegrationEventHandler<EntityChanged>>()));
        _ = app.CreateClient();
        var publisher = app.Services.GetRequiredService<IIntegrationEventPublisher>();
        await Should.ThrowAsync<InvalidOperationException>(() => publisher.PublishAsync(
            new EntityChanged("Probe", "Entity", "id", "Update", null, null, null, null, null), default));
    }
    [Fact]
    public async Task All_modules_have_applied_migrations_and_no_untracked_model_changes()
    {
        await factory.WithServiceAsync(async sp =>
        {
            foreach (var (type, _) in sp.GetRequiredService<ModuleDbContextRegistry>().Contexts)
            {
                var db = (DbContext)sp.GetRequiredService(type);
                (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
                db.Database.HasPendingModelChanges().ShouldBeFalse(type.Name);
            }
            return true;
        });
    }
    [Fact]
    public async Task Unknown_contract_is_inspectable_dead_letter_in_the_real_processor()
    {
        await using var isolated = new ApiFactory { OutboxEnabled = false };
        await isolated.InitializeAsync();
        var id = await isolated.WithServiceAsync(async sp =>
        {
            var db = sp.GetRequiredService<IdentityDbContext>();
            var message = new OutboxMessage { Type = "unsupported.contract.v999", Payload = "{}", OccurredOn = DateTimeOffset.UtcNow };
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
            return message.Id;
        });
        await using var worker = isolated.WithWebHostBuilder(b => b.UseSetting("Outbox:Enabled", "true").UseSetting("Outbox:MaxAttempts", "1"));
        _ = worker.CreateClient();
        var terminal = false;
        for (var i = 0; i < 50; i++)
        {
            terminal = await isolated.WithServiceAsync(sp => sp.GetRequiredService<IdentityDbContext>().OutboxMessages.AnyAsync(x => x.Id == id && x.DeadLetteredAt != null && x.ProcessedOn == null));
            if (terminal) break;
            await Task.Delay(200);
        }
        terminal.ShouldBeTrue();
    }
}
