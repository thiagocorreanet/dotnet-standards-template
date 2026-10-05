using System.Reflection;
using NetArchTest.Rules;
using Shared.Kernel.Results;
using Shouldly;
using Xunit;

namespace Tests.Architecture;

public sealed class ModuleBoundaryTests
{
    private static readonly string[] InfrastructureNamespaces =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql",
        "Shared.Http",
        "Shared.Data",
        "Shared.WebHost",
        "Shared.Messaging",
        "Shared.Observability",
    ];

    [Theory]
    [MemberData(nameof(ArchitectureTestData.Modules), MemberType = typeof(ArchitectureTestData))]
    public void Module_ShouldNotDependOnAnotherModule(Assembly moduleAssembly)
    {
        var ownNamespace = moduleAssembly.GetName().Name!;
        var forbiddenNamespaces = ArchitectureTestData.ModuleAssemblies
            .Select(assembly => assembly.GetName().Name!)
            .Where(moduleNamespace => moduleNamespace != ownNamespace)
            .ToArray();

        var result = Types.InAssembly(moduleAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        AssertSuccess(result, $"{ownNamespace} não pode referenciar diretamente outro Module.*; use Shared.Contracts");
    }

    [Theory]
    [MemberData(nameof(ArchitectureTestData.Modules), MemberType = typeof(ArchitectureTestData))]
    public void Domain_ShouldNotDependOnModuleUseCasesOrShared(Assembly moduleAssembly)
    {
        var moduleNamespace = moduleAssembly.GetName().Name!;
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .ResideInNamespaceStartingWith($"{moduleNamespace}.Domain")
            .ShouldNot()
            .HaveDependencyOnAny($"{moduleNamespace}.UseCases", $"{moduleNamespace}.Shared")
            .GetResult();

        AssertSuccess(result, $"O Domain de {moduleNamespace} deve permanecer independente das camadas externas");
    }

    [Theory]
    [MemberData(nameof(ArchitectureTestData.Modules), MemberType = typeof(ArchitectureTestData))]
    public void Domain_ShouldDependOnlyOnSystemKernelAndContracts(Assembly moduleAssembly)
    {
        var moduleNamespace = moduleAssembly.GetName().Name!;
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .ResideInNamespaceStartingWith($"{moduleNamespace}.Domain")
            .Should()
            // O rastreador do Coverlet é injetado nos assemblies quando a suíte coleta cobertura (CI e test-template).
            .OnlyHaveDependenciesOn("System", "Shared.Kernel", "Shared.Contracts", $"{moduleNamespace}.Domain", "Coverlet.Core.Instrumentation")
            .GetResult();

        AssertSuccess(result, $"O Domain de {moduleNamespace} só pode depender de System.*, Shared.Kernel e Shared.Contracts");
    }

    [Theory]
    [MemberData(nameof(ArchitectureTestData.Modules), MemberType = typeof(ArchitectureTestData))]
    public void Domain_ShouldNotDependOnInfrastructure(Assembly moduleAssembly)
    {
        var moduleNamespace = moduleAssembly.GetName().Name!;
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .ResideInNamespaceStartingWith($"{moduleNamespace}.Domain")
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespaces)
            .GetResult();

        AssertSuccess(result, $"O Domain de {moduleNamespace} não pode conhecer EF Core, ASP.NET Core nem a infraestrutura de Shared.*");
    }

    [Fact]
    public void Kernel_ShouldNotDependOnAspNetCoreOrEntityFramework()
    {
        var kernel = typeof(Result).Assembly;
        var forbidden = new[] { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql" };

        var references = kernel.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => forbidden.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();
        references.ShouldBeEmpty("Shared.Kernel não pode referenciar ASP.NET Core nem EF Core");

        var result = Types.InAssembly(kernel)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespaces)
            .GetResult();
        AssertSuccess(result, "Shared.Kernel não pode depender de infraestrutura");
    }

    [Theory]
    [MemberData(nameof(ArchitectureTestData.Modules), MemberType = typeof(ArchitectureTestData))]
    public void Module_ShouldNotDeclareRepository(Assembly moduleAssembly)
    {
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .AreClasses()
            .Should()
            .NotHaveNameMatching(".*Repository.*")
            .GetResult();

        AssertSuccess(result, "O acesso a dados deve usar o DbContext diretamente, sem repositórios");
    }

    private static void AssertSuccess(TestResult result, string rule) =>
        result.IsSuccessful.ShouldBeTrue(
            $"{rule}. Tipos em violação: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
