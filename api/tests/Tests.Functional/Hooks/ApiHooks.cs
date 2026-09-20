using Reqnroll;
using Tests.Integration.Infra;

namespace Tests.Functional.Hooks;

/// <summary>Uma única API (e um único PostgreSQL em container) para toda a execução dos testes funcionais.</summary>
[Binding]
public static class ApiHooks
{
    public static ApiFactory Api { get; private set; } = null!;

    [BeforeTestRun]
    public static async Task BeforeExecution()
    {
        Api = new ApiFactory();
        await Api.InitializeAsync();
        _ = Api.CreateClient(); // força a subida do host (migrações + seed)
    }

    [AfterTestRun]
    public static async Task AfterExecution() => await Api.DisposeAsync();
}
