using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data.Transactions;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;
using Shared.Observability.Telemetry;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;

namespace Tests.Integration.Reliability;

/// <summary>
/// Advisory lock real do PostgreSQL com a composição de produção (<c>AddUseCasesFromTypes</c>): os casos de uso de
/// teste seguram a trava até serem liberados e terminam com falha de negócio, então nada é gravado.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ConsistencyKeyLockTests(ApiFactory factory)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan Blocked = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task Commands_on_different_resources_run_in_parallel()
    {
        var (provider, gate) = Compose();
        await using var owned = provider;
        var first = Run(provider, new ResourceRequest(Guid.NewGuid(), "first"));
        await gate.Entered("first").WaitAsync(Timeout);

        var second = Run(provider, new ResourceRequest(Guid.NewGuid(), "second"));
        await gate.Entered("second").WaitAsync(Timeout);

        gate.Release("first");
        gate.Release("second");
        (await first).Error.Code.ShouldBe("Probe.RolledBack");
        (await second).Error.Code.ShouldBe("Probe.RolledBack");
    }

    [Fact]
    public async Task Commands_on_the_same_resource_wait_in_line()
    {
        var (provider, gate) = Compose();
        await using var owned = provider;
        var resource = Guid.NewGuid();
        var first = Run(provider, new ResourceRequest(resource, "first"));
        await gate.Entered("first").WaitAsync(Timeout);

        var second = Run(provider, new ResourceRequest(resource, "second"));
        (await Task.WhenAny(gate.Entered("second"), Task.Delay(Blocked))).ShouldNotBe(gate.Entered("second"));

        gate.Release("first");
        await first;
        await gate.Entered("second").WaitAsync(Timeout);
        gate.Release("second");
        await second;
    }

    [Fact]
    public async Task Command_without_key_queues_every_write_of_the_module()
    {
        var (provider, gate) = Compose();
        await using var owned = provider;
        var first = Run(provider, new ModuleRequest(Guid.NewGuid(), "first"));
        await gate.Entered("first").WaitAsync(Timeout);

        var second = Run(provider, new ModuleRequest(Guid.NewGuid(), "second"));
        (await Task.WhenAny(gate.Entered("second"), Task.Delay(Blocked))).ShouldNotBe(gate.Entered("second"));

        gate.Release("first");
        await first;
        await gate.Entered("second").WaitAsync(Timeout);
        gate.Release("second");
        await second;
    }

    private (ServiceProvider Provider, LockGate Gate) Compose()
    {
        var gate = new LockGate();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<CommandTransactionOptions>();
        services.AddSingleton<ICommandTransactionObserver, NullCommandTransactionObserver>();
        services.AddDbContext<ProbeDbContext>(options => options.UseNpgsql(factory.ConnectionString));
        services.AddSingleton(gate);
        services.AddUseCasesFromTypes(
            [typeof(ProbeDbContext), typeof(ResourceCommand), typeof(ResourcePolicy), typeof(ModuleCommand), typeof(ModulePolicy)],
            new ModuleTelemetry("ConsistencyProbe"));
        return (services.BuildServiceProvider(), gate);
    }

    private static Task<Result<string>> Run<TRequest>(IServiceProvider provider, TRequest request) => Task.Run(async () =>
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IUseCase<TRequest, string>>().HandleAsync(request, CancellationToken.None);
    });

    private sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options);

    private sealed record ResourceRequest(Guid ResourceId, string Invocation);

    private sealed record ModuleRequest(Guid ResourceId, string Invocation);

    [Command("probe:{ResourceId}")]
    private sealed class ResourceCommand(LockGate gate) : IUseCase<ResourceRequest, string>
    {
        public Task<Result<string>> HandleAsync(ResourceRequest request, CancellationToken cancellationToken) =>
            gate.HoldAsync(request.Invocation, cancellationToken);
    }

    [Command]
    private sealed class ModuleCommand(LockGate gate) : IUseCase<ModuleRequest, string>
    {
        public Task<Result<string>> HandleAsync(ModuleRequest request, CancellationToken cancellationToken) =>
            gate.HoldAsync(request.Invocation, cancellationToken);
    }

    private sealed class ResourcePolicy : IAccessPolicy<ResourceRequest>
    {
        public Task<bool> CanExecuteAsync(ResourceRequest request, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class ModulePolicy : IAccessPolicy<ModuleRequest>
    {
        public Task<bool> CanExecuteAsync(ModuleRequest request, CancellationToken ct) => Task.FromResult(true);
    }

    /// <summary>Sinaliza a entrada de cada invocação (trava obtida) e a segura até <see cref="Release"/>.</summary>
    private sealed class LockGate
    {
        private readonly ConcurrentDictionary<string, (TaskCompletionSource Entered, TaskCompletionSource Release)> _invocations = new();

        public Task Entered(string invocation) => Get(invocation).Entered.Task;

        public void Release(string invocation) => Get(invocation).Release.TrySetResult();

        public async Task<Result<string>> HoldAsync(string invocation, CancellationToken ct)
        {
            var (entered, release) = Get(invocation);
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            return Error.Conflict("Probe.RolledBack", "Falha de negócio: a transação é desfeita e a trava, liberada.");
        }

        private (TaskCompletionSource Entered, TaskCompletionSource Release) Get(string invocation) =>
            _invocations.GetOrAdd(invocation, _ => (
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)));
    }
}
