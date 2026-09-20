using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Module.Identity.Domain;
using Module.Identity.Shared;
using Shared.Data.Transactions;
using Testcontainers.PostgreSql;
using Xunit;
namespace Tests.Integration.Infra;
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("modular_api_tests").WithUsername("test_user").WithPassword(Guid.NewGuid().ToString("N")).Build();
    public string ConnectionString => postgres.GetConnectionString();
    public Guid DefaultAccountId { get; private set; }
    public FaultObserver Faults { get; } = new();
    public TestIdentityProvider IdentityProvider { get; } = new();
    public bool OutboxEnabled { get; init; } = true;
    public Task StartDatabaseAsync() => postgres.StartAsync();
    public async Task InitializeAsync()
    {
        await StartDatabaseAsync();
        _ = CreateClient();
        DefaultAccountId = await CreateIdentityAsync(TestIdentityProvider.DefaultSubject);
    }
    public new async Task DisposeAsync() { await base.DisposeAsync(); await postgres.DisposeAsync(); }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:ModularApi", ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Oidc:Authority", TestIdentityProvider.Issuer);
        builder.UseSetting("Oidc:Audience", TestIdentityProvider.Audience);
        builder.UseSetting("Oidc:RequireHttpsMetadata", "true");
        builder.UseSetting("OpenApi:Enabled", "true");
        builder.UseSetting("Outbox:PollingIntervalMs", "200");
        builder.UseSetting("Outbox:Enabled", OutboxEnabled.ToString());
        builder.UseSetting("RateLimiting:PermitLimit", "100000");
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
        builder.ConfigureServices(services =>
        {
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o => o.BackchannelHttpHandler = IdentityProvider);
            services.RemoveAll<ICommandTransactionObserver>();
            services.AddSingleton<ICommandTransactionObserver>(Faults);
            services.ConfigureDbContext<IdentityDbContext>(options => options.AddInterceptors(Faults));
        });
    }
    public async Task<Guid> CreateIdentityAsync(string subject) => await WithServiceAsync(async sp =>
    {
        var db = sp.GetRequiredService<IdentityDbContext>();
        var user = User.Create(TestIdentityProvider.Issuer, subject, "Test account", "account@example.test");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    });
    public async Task<T> WithServiceAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }
}
public sealed class FaultObserver : SaveChangesInterceptor, ICommandTransactionObserver
{
    private int saving;
    private int before;
    private int after;
    public int BeforeCalls { get; private set; }
    public void ArmBeforeSave() { BeforeCalls = 0; Interlocked.Exchange(ref saving, 1); }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref saving, 0) == 1) throw new TimeoutException("InjectedBeforePersistence");
        return ValueTask.FromResult(result);
    }
    public void ArmBefore() { BeforeCalls = 0; Interlocked.Exchange(ref before, 1); }
    public void ArmAfter() { BeforeCalls = 0; Interlocked.Exchange(ref after, 1); }
    public Task BeforeCommitAsync(Guid operationId, int attempt, CancellationToken cancellationToken)
    {
        BeforeCalls++;
        if (Interlocked.Exchange(ref before, 0) == 1) throw new TimeoutException("InjectedBeforeCommit");
        return Task.CompletedTask;
    }
    public Task AfterCommitAsync(Guid operationId, int attempt, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref after, 0) == 1) throw new TimeoutException("InjectedAfterCommit");
        return Task.CompletedTask;
    }
}
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory> { public const string Name = "Api"; }
