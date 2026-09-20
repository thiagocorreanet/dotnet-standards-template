using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Module.Identity.UseCases.RegisterUser;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;

namespace Tests.Integration.Contracts;

[Collection(ApiCollection.Name)]
public sealed class LanguageConventionTests(ApiFactory factory)
{
    [Fact]
    public async Task Identity_payload_pagination_and_roles_use_English_contracts()
    {
        using var client = factory.AuthenticatedClient();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/identity/users/me");
        me.EnumerateObject().Select(property => property.Name).Order().ShouldBe(new[] { "id", "roles", "userEmail", "userName" }.Order());
        me.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ShouldContain("Administrator");

        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/identity/users?page=1&pageSize=1");
        page.EnumerateObject().Select(property => property.Name).Order().ShouldBe(new[] { "items", "page", "pageSize", "total", "totalPages" }.Order());
        page.GetProperty("pageSize").GetInt32().ShouldBe(1);
        page.GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Problem_codes_and_field_names_are_English_but_messages_are_Portuguese()
    {
        using var client = factory.AuthenticatedClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US");
        using var response = await client.PostAsJsonAsync("/api/v1/identity/users",
            new { subject = "", userName = "", userEmail = "invalid" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().ShouldBe("Validation");
        problem.TryGetProperty("codigo", out _).ShouldBeFalse();
        problem.GetProperty("title").GetString().ShouldBe("Requisição inválida");
        problem.GetProperty("errors").GetProperty("Subject")[0].ToString().ShouldContain("deve ser informado");
        problem.GetProperty("errors").GetProperty("UserEmail")[0].ToString().ShouldContain("válido");
    }

    [Fact]
    public async Task Default_validator_messages_remain_Portuguese_under_English_process_culture()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            await factory.WithServiceAsync(async services =>
            {
                var result = await services.GetRequiredService<IValidator<RegisterUserRequest>>()
                    .ValidateAsync(new RegisterUserRequest("", "", "invalid"));
                result.Errors.ShouldContain(error => error.ErrorMessage.Contains("deve ser informado", StringComparison.Ordinal));
                result.Errors.ShouldNotContain(error => error.ErrorMessage.Contains("must not", StringComparison.Ordinal));
                return true;
            });
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public async Task OpenApi_keeps_Portuguese_prose_and_English_operation_names()
    {
        using var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        document.GetProperty("info").GetProperty("description").ToString().ShouldContain("Cada módulo possui seu schema");
        document.GetProperty("paths").GetProperty("/api/v1/identity/users").GetProperty("post")
            .GetProperty("operationId").GetString().ShouldBe("RegisterUser");
        foreach (var legacyPath in new[] { "/api/v1/identidade/usuarios", "/api/v1/auditoria/registros" })
            document.GetProperty("paths").TryGetProperty(legacyPath, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Legacy_Portuguese_role_does_not_grant_administrative_access()
    {
        using var client = factory.AuthenticatedClient("Administrador");
        using var response = await client.GetAsync("/api/v1/identity/users");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("title").GetString().ShouldBe("Acesso negado");
    }

    [Theory]
    [InlineData("/api/v1/identity/users/me", HttpStatusCode.Unauthorized, "Não autenticado")]
    [InlineData("/missing-resource", HttpStatusCode.NotFound, "Recurso não encontrado")]
    public async Task Middleware_status_messages_use_Portuguese(string path, HttpStatusCode status, string title)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        response.StatusCode.ShouldBe(status);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("title").GetString().ShouldBe(title);
    }
}
