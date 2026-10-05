using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Http.Endpoints;
using Shared.Kernel.Results;
using Shared.Observability.Telemetry;
using Shouldly;

namespace Tests.Unit.Shared;

public sealed class AccessPolicyCompositionTests
{
    private static readonly ModuleTelemetry Telemetry = new("Probe");

    [Fact]
    public void Use_case_without_policy_fails_at_composition()
    {
        var error = Should.Throw<InvalidOperationException>(() =>
            new ServiceCollection().AddUseCasesFromTypes([typeof(ProbeDbContext), typeof(ProbeUseCase)], Telemetry));
        error.Message.ShouldContain("sem política de acesso");
        error.Message.ShouldContain(nameof(ProbeRequest));
    }

    [Fact]
    public void Use_case_with_two_policies_fails_at_composition()
    {
        var error = Should.Throw<InvalidOperationException>(() => new ServiceCollection().AddUseCasesFromTypes(
            [typeof(ProbeDbContext), typeof(ProbeUseCase), typeof(AllowPolicy), typeof(DenyPolicy)], Telemetry));
        error.Message.ShouldContain("mais de uma");
    }

    [Fact]
    public void Policy_of_the_slice_is_registered_for_the_request()
    {
        var services = new ServiceCollection().AddUseCasesFromTypes(
            [typeof(ProbeDbContext), typeof(ProbeUseCase), typeof(DenyPolicy)], Telemetry);

        var descriptor = services.Single(d => d.ServiceType == typeof(IAccessPolicy<ProbeRequest>));
        descriptor.ImplementationType.ShouldBe(typeof(DenyPolicy));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public async Task Denied_policy_returns_resource_denied_without_running_the_use_case()
    {
        var inner = new ProbeUseCase();
        var decorator = new AuthorizedUseCaseDecorator<ProbeRequest, string>(inner, new DenyPolicy());

        var result = await decorator.HandleAsync(new ProbeRequest(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Authorization.ResourceDenied");
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        inner.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Allowed_policy_runs_the_use_case()
    {
        var inner = new ProbeUseCase();
        var decorator = new AuthorizedUseCaseDecorator<ProbeRequest, string>(inner, new AllowPolicy());

        var result = await decorator.HandleAsync(new ProbeRequest(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        inner.Calls.ShouldBe(1);
    }

    private sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options);

    private sealed record ProbeRequest;

    private sealed class ProbeUseCase : IUseCase<ProbeRequest, string>
    {
        public int Calls { get; private set; }

        public Task<Result<string>> HandleAsync(ProbeRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Result<string>>("ok");
        }
    }

    private sealed class AllowPolicy : IAccessPolicy<ProbeRequest>
    {
        public Task<bool> CanExecuteAsync(ProbeRequest request, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class DenyPolicy : IAccessPolicy<ProbeRequest>
    {
        public Task<bool> CanExecuteAsync(ProbeRequest request, CancellationToken ct) => Task.FromResult(false);
    }
}
