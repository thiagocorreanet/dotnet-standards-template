using Shared.Http.Endpoints;

namespace Module.Talks.UseCases.ValidateCertificate;

/// <summary>Consulta pública: o endpoint é anônimo e a resposta oculta o titular do certificado.</summary>
internal sealed class ValidateCertificateAccessPolicy : IAccessPolicy<ValidateCertificateRequest>
{
    public Task<bool> CanExecuteAsync(ValidateCertificateRequest request, CancellationToken ct) =>
        Task.FromResult(true);
}
