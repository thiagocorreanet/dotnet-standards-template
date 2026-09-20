using System.Net;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Tests.Integration.Infra;
using Xunit;
namespace Tests.Integration.Security;
public sealed class JwksLifecycleTests
{
    [Fact]
    public async Task Unknown_key_triggers_JWKS_refresh_and_accepts_rotated_signing_key()
    {
        await using var factory = new ApiFactory { OutboxEnabled = false };
        await factory.InitializeAsync();
        using var client = factory.AuthenticatedClient();
        (await client.GetAsync("/api/v1/identity/users/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var rsa = RSA.Create(2048);
        var next = new RsaSecurityKey(rsa) { KeyId = "rotated-key" };
        factory.IdentityProvider.PublishedKeys = [TestIdentityProvider.Key, next];
        client.DefaultRequestHeaders.Authorization = new("Bearer", TestToken.Generate(key: next));
        HttpStatusCode status = default;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!timeout.IsCancellationRequested)
        {
            status = (await client.GetAsync("/api/v1/identity/users/me")).StatusCode;
            if (status == HttpStatusCode.OK) break;
            await Task.Delay(200);
        }
        status.ShouldBe(HttpStatusCode.OK);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdP_outage_never_bypasses_validation_and_only_warm_JWKS_cache_works(bool warm)
    {
        await using var factory = new ApiFactory { OutboxEnabled = false };
        await factory.InitializeAsync();
        using var client = factory.AuthenticatedClient();
        if (warm) (await client.GetAsync("/api/v1/identity/users/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        factory.IdentityProvider.Available = false;
        (await client.GetAsync("/api/v1/identity/users/me")).StatusCode.ShouldBe(warm ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
    }
}
