using System.Net.Http.Json;
using System.Text.Json;
using Shared.Contracts.Identity;
using Tests.Functional.Hooks;
using Tests.Integration.Infra;

namespace Tests.Functional.Steps;

/// <summary>Estado compartilhado entre os passos de um cenário (injeção de contexto do Reqnroll).</summary>
public sealed class ScenarioContext
{
    private HttpClient? _client;

    public HttpClient Client => _client ??= ApiHooks.Api.AuthenticatedClient(DefaultRoles.Administrator, "Organizador Funcional");
    public HttpResponseMessage? LastResponse { get; set; }
    public Dictionary<string, Guid> Ids { get; } = [];
    public string Suffix { get; } = Guid.NewGuid().ToString("N")[..8];

    private bool identityCreated;
    public async Task AuthenticateAs(string role)
    {
        var subject = "functional|" + Suffix;
        if (!identityCreated) { await ApiHooks.Api.CreateIdentityAsync(subject); identityCreated = true; }
        _client = ApiHooks.Api.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new("Bearer", TestToken.Generate(role, subject: subject));
    }
    public async Task<HttpResponseMessage> PostAsAdministratorAsync(string url, object body)
    {
        using var admin = ApiHooks.Api.AuthenticatedClient();
        LastResponse = await admin.PostAsJsonAsync(url, body);
        return LastResponse;
    }

    public string UniqueName(string name) => $"{name} [{Suffix}]";

    public async Task<JsonElement> JsonBodyAsync()
    {
        var text = await LastResponse!.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public async Task<HttpResponseMessage> PostAsync(string url, object body)
    {
        LastResponse = await Client.PostAsJsonAsync(url, body);
        return LastResponse;
    }

    public async Task<HttpResponseMessage> PostAnonymouslyAsync(string url, object body)
    {
        LastResponse = await ApiHooks.Api.CreateClient().PostAsJsonAsync(url, body);
        return LastResponse;
    }

    public async Task<HttpResponseMessage> PutAsync(string url, object body)
    {
        LastResponse = await Client.PutAsJsonAsync(url, body);
        return LastResponse;
    }

    public async Task<HttpResponseMessage> PatchAsync(string url, object body)
    {
        LastResponse = await Client.PatchAsJsonAsync(url, body);
        return LastResponse;
    }

    public async Task<HttpResponseMessage> GetAsync(string url)
    {
        LastResponse = await Client.GetAsync(url);
        return LastResponse;
    }

    public async Task<HttpResponseMessage> DeleteAsync(string url)
    {
        LastResponse = await Client.DeleteAsync(url);
        return LastResponse;
    }
}
