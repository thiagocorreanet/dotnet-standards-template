using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;

namespace Tests.Integration.Contracts;

[Collection(ApiCollection.Name)]
public sealed class ScalarDocumentationTests(ApiFactory factory)
{
    [Fact]
    public async Task Root_redirects_to_scalar_and_swagger_is_removed()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/");
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.ShouldBe("/scalar");
        using var swagger = await client.GetAsync("/swagger/index.html");
        swagger.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/scalar")]
    [InlineData("/scalar/v1")]
    public async Task Anonymous_ui_uses_local_contract_without_persisting_tokens(string path)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("Modular API");
        // O helper local do Scalar resolve o documento relativo à raiz da aplicação.
        html.ShouldContain("\"url\":\"openapi/v1.json\"");
        html.ShouldContain("src=\"scalar.js\"");
        html.ShouldContain("src=\"scalar.aspnetcore.js\"");
        html.ShouldContain("Bearer");
        html.ShouldMatch("\"persistAuth\"\\s*:\\s*false");
        html.ShouldMatch("\"withDefaultFonts\"\\s*:\\s*false");
        html.ShouldMatch("\"telemetry\"\\s*:\\s*false");
        html.ShouldMatch("\"agent\"\\s*:\\s*\\{\\s*\"disabled\"\\s*:\\s*true");
        html.ShouldNotContain("cdn.jsdelivr.net");
        html.ShouldNotContain("clientSecret");
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
    }

    [Theory]
    [InlineData("/scalar/scalar.js", "javascript")]
    [InlineData("/scalar/scalar.aspnetcore.js", "javascript")]
    [InlineData("/scalar/favicon.svg", "image/svg+xml")]
    public async Task Assets_are_served_locally_without_authentication(string path, string contentType)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (response.Content.Headers.ContentType?.MediaType ?? string.Empty).ShouldContain(contentType);
        (await response.Content.ReadAsByteArrayAsync()).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Disabled_flag_removes_ui_assets_contracts_and_redirect()
    {
        await using var disabled = factory.WithWebHostBuilder(builder => builder.UseSetting("OpenApi:Enabled", "false"));
        using var client = disabled.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        foreach (var path in new[] { "/", "/scalar", "/scalar/v1", "/scalar/scalar.js", "/scalar/scalar.aspnetcore.js", "/scalar/favicon.svg", "/openapi/v1.json", "/openapi/v1.yaml" })
        {
            using var response = await client.GetAsync(path);
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound, path);
        }
    }
}
