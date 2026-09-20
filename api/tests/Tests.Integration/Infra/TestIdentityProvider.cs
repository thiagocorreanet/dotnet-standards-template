using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
namespace Tests.Integration.Infra;
/// <summary>Discovery e JWKS reais no protocolo, transporte em memória. Não substitui o handler JWT da aplicação.</summary>
public sealed class TestIdentityProvider : HttpMessageHandler
{
    public const string Issuer = "https://identity.test/realms/modular-api";
    public const string Audience = "modular-api";
    public const string DefaultSubject = "federated|test-administrator";
    public static readonly RSA Rsa = RSA.Create(2048);
    public static readonly RsaSecurityKey Key = new(Rsa) { KeyId = "test-key-1" };
    public bool Available { get; set; } = true;
    public RsaSecurityKey[] PublishedKeys { get; set; } = [Key];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!Available) throw new HttpRequestException("SimulatedIdpUnavailable");
        object content = request.RequestUri!.AbsolutePath.EndsWith("/certs", StringComparison.Ordinal)
            ? new { keys = PublishedKeys.Select(key => JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(key.Rsa.ExportParameters(false)) { KeyId = key.KeyId })).ToArray() }
            : new { issuer = Issuer, jwks_uri = Issuer + "/certs", authorization_endpoint = Issuer + "/auth", token_endpoint = Issuer + "/token", id_token_signing_alg_values_supported = new[] { "RS256" } };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json")
        });
    }
}
