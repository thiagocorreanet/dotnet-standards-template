using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;
using Shared.Observability.Telemetry;
using Shouldly;

namespace Tests.Unit.Shared;

public sealed class ConsistencyKeyTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();

    [Fact]
    public void Command_without_key_uses_the_module_name()
    {
        var key = ConsistencyKey.For(typeof(ProbeUseCase), typeof(ProbeRequest), new CommandAttribute(), "Probe");

        key.Resolve(new ProbeRequest(ResourceId, "x", 1)).ShouldBe("Probe");
    }

    [Fact]
    public void Fixed_key_keeps_the_lock_identifier_of_previous_versions()
    {
        var key = ConsistencyKey.For(typeof(ProbeUseCase), typeof(ProbeRequest), new CommandAttribute("event-management-example"), "Probe");
        var resolved = key.Resolve(new ProbeRequest(ResourceId, "x", 1));
        var legacy = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes("event-management-example")));

        resolved.ShouldBe("event-management-example");
        ConsistencyKey.LockIdOf(resolved).ShouldBe(legacy);
    }

    [Fact]
    public void Placeholders_are_resolved_from_request_properties()
    {
        var key = ConsistencyKey.For(typeof(ProbeUseCase), typeof(ProbeRequest), new CommandAttribute("probe:{ResourceId}:{Code}"), "Probe");

        key.Resolve(new ProbeRequest(ResourceId, "abc", 1)).ShouldBe($"probe:{ResourceId:D}:ABC");
        key.Resolve(new ProbeRequest(ResourceId, "ABC", 1)).ShouldBe(key.Resolve(new ProbeRequest(ResourceId, "abc", 1)));
        key.Resolve(new ProbeRequest(Guid.NewGuid(), "abc", 1)).ShouldNotBe(key.Resolve(new ProbeRequest(ResourceId, "abc", 1)));
    }

    [Theory]
    [InlineData("probe:{Missing}")]
    [InlineData("probe:{Count}")]
    [InlineData("probe:{resourceId}")]
    [InlineData("probe:{}")]
    [InlineData("probe:{ResourceId")]
    [InlineData("probe:ResourceId}")]
    [InlineData("probe:{Resource{Id}}")]
    [InlineData(" ")]
    public void Invalid_template_fails_at_composition(string template) =>
        Should.Throw<InvalidOperationException>(() =>
            ConsistencyKey.For(typeof(ProbeUseCase), typeof(ProbeRequest), new CommandAttribute(template), "Probe"));

    [Fact]
    public void Empty_resource_identifier_is_a_violated_contract_not_a_coarser_lock()
    {
        var key = ConsistencyKey.For(typeof(ProbeUseCase), typeof(ProbeRequest), new CommandAttribute("probe:{ResourceId}:{Code}"), "Probe");

        Should.Throw<InvalidOperationException>(() => key.Resolve(new ProbeRequest(Guid.Empty, "abc", 1)));
        Should.Throw<InvalidOperationException>(() => key.Resolve(new ProbeRequest(ResourceId, " ", 1)));
        Should.Throw<InvalidOperationException>(() => key.Resolve(null));
    }

    [Fact]
    public void Invalid_placeholder_stops_use_case_registration()
    {
        var error = Should.Throw<InvalidOperationException>(() => new ServiceCollection().AddUseCasesFromTypes(
            [typeof(ProbeDbContext), typeof(BrokenCommandUseCase), typeof(ProbePolicy)], new ModuleTelemetry("Probe")));

        error.Message.ShouldContain("{Missing}");
    }

    private sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options);

    private sealed record ProbeRequest(Guid ResourceId, string Code, int Count);

    private sealed class ProbeUseCase : IUseCase<ProbeRequest, string>
    {
        public Task<Result<string>> HandleAsync(ProbeRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<Result<string>>("ok");
    }

    [Command("probe:{Missing}")]
    private sealed class BrokenCommandUseCase : IUseCase<ProbeRequest, string>
    {
        public Task<Result<string>> HandleAsync(ProbeRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<Result<string>>("ok");
    }

    private sealed class ProbePolicy : IAccessPolicy<ProbeRequest>
    {
        public Task<bool> CanExecuteAsync(ProbeRequest request, CancellationToken ct) => Task.FromResult(true);
    }
}
