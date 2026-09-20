using Shared.WebHost;
using Shared.Data.Migrations;
using Shared.Contracts.Identity;

if (args.FirstOrDefault() == "healthcheck")
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        client.DefaultRequestHeaders.Host = Environment.GetEnvironmentVariable("AllowedHosts")?.Split(';')[0] ?? "localhost";
        Environment.ExitCode = (await client.GetAsync("http://localhost:8080/health/live")).IsSuccessStatusCode ? 0 : 1;
    }
    catch (Exception) { Environment.ExitCode = 1; }
    return;
}
var command = args.FirstOrDefault() is "migrate" or "bootstrap-identity" ? args[0] : null;
var builder = WebApplication.CreateBuilder(command is null ? args : args[1..]);
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);
builder.AddModularWebHost("ModularApi.Api");
var app = builder.Build();
if (command == "migrate")
{
    await app.Services.GetRequiredService<DatabaseMigrationHostedService>().RunAsync(CancellationToken.None);
    await RuntimeDatabasePrivileges.GrantAsync(app.Services,
        app.Services.GetRequiredService<ModuleDbContextRegistry>(),
        app.Configuration["Database:RuntimeRole"] ?? "", CancellationToken.None);
    return;
}
if (command == "bootstrap-identity")
{
    await using var scope = app.Services.CreateAsyncScope();
    string Required(string key) => app.Configuration["Bootstrap:" + key] ?? throw new InvalidOperationException("Configure Bootstrap:" + key);
    var id = await scope.ServiceProvider.GetRequiredService<IIdentityBootstrapper>()
        .ProvisionFirstAsync(Required("Subject"), Required("Name"), Required("Email"), CancellationToken.None);
    Console.WriteLine($"Vínculo inicial criado: {id}. O perfil administrativo deve existir no provedor OIDC.");
    return;
}
app.UseModularWebHost();
app.Run();
public partial class Program;
