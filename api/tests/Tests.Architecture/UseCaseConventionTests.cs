using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NetArchTest.Rules;
using Shared.Http.Endpoints;
using Shared.WebHost.Modules;
using Shouldly;
using Xunit;

namespace Tests.Architecture;

public sealed class UseCaseConventionTests
{
    [Theory]
    [MemberData(nameof(ArchitectureTestData.Modules), MemberType = typeof(ArchitectureTestData))]
    public void UseCases_ShouldImplementUseCaseContract(System.Reflection.Assembly moduleAssembly)
    {
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .HaveNameEndingWith("UseCase")
            .Should()
            .ImplementInterface(typeof(IUseCase<,>))
            .GetResult();

        AssertSuccess(result, "Classes *UseCase devem implementar IUseCase<TRequest, TResponse>");
    }

    [Theory]
    [MemberData(nameof(ArchitectureTestData.Modules), MemberType = typeof(ArchitectureTestData))]
    public void Endpoints_ShouldImplementEndpointContract(System.Reflection.Assembly moduleAssembly)
    {
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .HaveNameEndingWith("Endpoint")
            .Should()
            .ImplementInterface(typeof(IEndpoint))
            .GetResult();

        AssertSuccess(result, "Classes *Endpoint devem implementar IEndpoint");
    }

    [Theory]
    [MemberData(nameof(ArchitectureTestData.Modules), MemberType = typeof(ArchitectureTestData))]
    public void Validators_ShouldInheritFromFluentValidation(System.Reflection.Assembly moduleAssembly)
    {
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .HaveNameEndingWith("Validator")
            .Should()
            .Inherit(typeof(AbstractValidator<>))
            .GetResult();

        AssertSuccess(result, "Classes *Validator devem herdar de AbstractValidator<T>");
    }

    [Theory]
    [MemberData(nameof(ArchitectureTestData.Modules), MemberType = typeof(ArchitectureTestData))]
    public void UseCases_ShouldBeInUseCasesNamespace(System.Reflection.Assembly moduleAssembly)
    {
        var moduleNamespace = moduleAssembly.GetName().Name!;
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .HaveNameEndingWith("UseCase")
            .Or()
            .HaveNameEndingWith("Endpoint")
            .Or()
            .HaveNameEndingWith("Validator")
            .Or()
            .HaveNameEndingWith("AccessPolicy")
            .Should()
            .ResideInNamespaceStartingWith($"{moduleNamespace}.UseCases")
            .GetResult();

        AssertSuccess(result, "UseCase, Endpoint, Validator e AccessPolicy devem permanecer em UseCases/<CasoDeUso>");
    }

    [Theory]
    [MemberData(nameof(ArchitectureTestData.Modules), MemberType = typeof(ArchitectureTestData))]
    public void AccessPolicies_ShouldImplementAccessPolicyContract(System.Reflection.Assembly moduleAssembly)
    {
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .HaveNameEndingWith("AccessPolicy")
            .Should()
            .ImplementInterface(typeof(IAccessPolicy<>))
            .GetResult();

        AssertSuccess(result, "Classes *AccessPolicy devem implementar IAccessPolicy<TRequest>");
    }

    [Theory]
    [MemberData(nameof(ArchitectureTestData.Modules), MemberType = typeof(ArchitectureTestData))]
    public void UseCases_ShouldHaveExactlyOneAccessPolicyInTheSlice(System.Reflection.Assembly moduleAssembly)
    {
        var types = moduleAssembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }).ToArray();
        var violations = types
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IUseCase<,>))
                .Select(i => (UseCase: t, Request: i.GenericTypeArguments[0])))
            .Select(u => (u.UseCase, Policies: types.Where(t => typeof(IAccessPolicy<>).MakeGenericType(u.Request).IsAssignableFrom(t)).ToArray()))
            .Where(u => u.Policies.Length != 1 || u.Policies[0].Namespace != u.UseCase.Namespace)
            .Select(u => $"{u.UseCase.Name} ({u.Policies.Length} policies)")
            .ToArray();

        violations.ShouldBeEmpty("Cada caso de uso precisa de exatamente uma IAccessPolicy<TRequest> no namespace do próprio slice");
    }

    [Theory]
    [MemberData(nameof(ArchitectureTestData.Modules), MemberType = typeof(ArchitectureTestData))]
    public void ModuleInfrastructure_ShouldFollowSuffixConventions(System.Reflection.Assembly moduleAssembly)
    {
        var types = moduleAssembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && !t.IsNested).ToArray();
        var violations = types
            .Where(t => typeof(DbContext).IsAssignableFrom(t) && !t.Name.EndsWith("DbContext", StringComparison.Ordinal)
                || typeof(IModule).IsAssignableFrom(t) && !t.Name.EndsWith("Module", StringComparison.Ordinal)
                || t.GetInterfaces().Any(i => i.Namespace?.StartsWith("Shared.Contracts", StringComparison.Ordinal) == true
                    && i.Name.EndsWith("ModuleApi", StringComparison.Ordinal)) && !t.Name.EndsWith("ModuleApi", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToArray();

        violations.ShouldBeEmpty("DbContext, IModule e contratos I*ModuleApi devem usar os sufixos *DbContext, *Module e *ModuleApi");
    }

    private static void AssertSuccess(TestResult result, string rule) =>
        result.IsSuccessful.ShouldBeTrue(
            $"{rule}. Tipos em violação: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
