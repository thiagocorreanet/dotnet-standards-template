using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Module.RateLimitProbe;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;

namespace Tests.Integration.Security;

/// <summary>
/// Cada cenário sobe um host próprio (limiters zerados) sobre o mesmo PostgreSQL. No TestServer não há IP remoto: os anônimos
/// caem na mesma partição <c>ip:unknown</c>.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RateLimitingTests(ApiFactory factory)
{
    private const string Me = "/api/v1/identity/users/me";

    [Fact]
    public async Task Global_limit_rejection_carries_numeric_retry_after()
    {
        await using var host = Host(("RateLimiting:AuthenticatedPermitLimit", "2"), ("RateLimiting:WindowSeconds", "60"));
        using var client = Authenticated(host);
        for (var i = 0; i < 2; i++)
            (await client.GetAsync(Me)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var rejected = await client.GetAsync(Me);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        AssertNumericRetryAfter(rejected, maximumSeconds: 60);
        (await ProblemTypeAsync(rejected)).ShouldBe("urn:problem:RateLimitExceeded");
    }

    [Fact]
    public async Task Health_checks_stay_available_and_do_not_consume_the_ingress_limit()
    {
        await using var host = Host(("RateLimiting:AnonymousPermitLimit", "1"), ("RateLimiting:IngressPermitLimit", "3"));
        using var client = host.CreateClient();
        (await client.GetAsync(Me)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using (var exhausted = await client.GetAsync(Me))
            exhausted.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        for (var i = 0; i < 5; i++)
        {
            (await client.GetAsync("/health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await client.GetAsync("/health/ready")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Terceira requisição comum: se as sondas contassem no ingresso, o 429 viria dele, não do limite global.
        using var after = await client.GetAsync(Me);
        after.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await ProblemTypeAsync(after)).ShouldBe("urn:problem:RateLimitExceeded");
    }

    [Fact]
    public async Task Anonymous_and_authenticated_requests_use_distinct_limits()
    {
        await using var host = Host(("RateLimiting:AuthenticatedPermitLimit", "3"), ("RateLimiting:AnonymousPermitLimit", "1"));
        using var anonymous = host.CreateClient();
        using var authenticated = Authenticated(host);

        (await anonymous.GetAsync(Me)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync(Me)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        for (var i = 0; i < 3; i++)
            (await authenticated.GetAsync(Me)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await authenticated.GetAsync(Me)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Named_policy_limits_its_endpoint_independently_of_the_global_limit()
    {
        await using var host = Host();
        using var client = Authenticated(host);
        const string probe = "/api/v1/rate-limit-probe/ping";
        for (var i = 0; i < RateLimitProbeModule.PermitLimit; i++)
            (await client.GetAsync(probe)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var rejected = await client.GetAsync(probe);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        AssertNumericRetryAfter(rejected, maximumSeconds: 60);
        (await client.GetAsync(Me)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private WebApplicationFactory<Program> Host(params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings) builder.UseSetting(key, value);
        });

    private static HttpClient Authenticated(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", TestToken.Generate());
        return client;
    }

    private static void AssertNumericRetryAfter(HttpResponseMessage response, int maximumSeconds)
    {
        var raw = response.Headers.GetValues("Retry-After").Single();
        raw.ShouldMatch("^[0-9]+$");
        int.Parse(raw, System.Globalization.CultureInfo.InvariantCulture).ShouldBeInRange(1, maximumSeconds);
    }

    private static async Task<string?> ProblemTypeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString();
}
