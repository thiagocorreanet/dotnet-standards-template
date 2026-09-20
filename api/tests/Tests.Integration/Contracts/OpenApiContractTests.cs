using System.Text.Json;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;
namespace Tests.Integration.Contracts;
[Collection(ApiCollection.Name)]
public sealed class OpenApiContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Contract_excludes_local_login_and_requires_bearer_for_protected_operations()
    {
        using var doc = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/openapi/v1.json"));
        var paths = doc.RootElement.GetProperty("paths");
        var bearer = doc.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        bearer.GetProperty("type").GetString().ShouldBe("http");
        bearer.GetProperty("scheme").GetString().ShouldBe("bearer");
        bearer.GetProperty("bearerFormat").GetString().ShouldBe("JWT");
        paths.EnumerateObject().ShouldNotContain(path => path.Name.StartsWith("/scalar", StringComparison.Ordinal));
        paths.TryGetProperty("/", out _).ShouldBeFalse();
        paths.TryGetProperty("/api/v1/identity/sessions", out _).ShouldBeFalse();
        paths.TryGetProperty("/api/v1/operations/outbox", out _).ShouldBeTrue();
        paths.TryGetProperty("/api/v1/identity/users/me", out _).ShouldBeTrue();
        foreach (var path in paths.EnumerateObject())
        foreach (var operation in path.Value.EnumerateObject().Where(x => new[] { "get", "post", "put", "patch", "delete" }.Contains(x.Name)))
        {
            operation.Value.GetProperty("operationId").GetString().ShouldNotBeNullOrWhiteSpace();
            operation.Value.GetProperty("tags").GetArrayLength().ShouldBe(1);
            if (!path.Name.Contains("/certificates/{code}", StringComparison.Ordinal))
                operation.Value.GetProperty("security").GetArrayLength().ShouldBeGreaterThan(0);
        }
    }
}
