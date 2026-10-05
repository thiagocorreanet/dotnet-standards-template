namespace Shared.Http.Endpoints;

/// <summary>
/// Decide se o usuário corrente pode executar o caso de uso de <typeparamref name="TRequest"/> sobre o recurso indicado.
/// Cada caso de uso tem exatamente uma policy, no diretório do próprio slice; a composição falha no startup sem ela.
/// Roda dentro da fronteira transacional nos comandos; <c>RequireAuthorization()</c> no endpoint é só barreira adicional.
/// </summary>
/// <remarks>
/// Retorne <c>false</c> para negar: o decorator responde 403 <c>Authorization.ResourceDenied</c>.
/// Regra repetida entre policies vira serviço pequeno em <c>Module.&lt;Name&gt;/Shared/</c>, não classe base.
/// </remarks>
public interface IAccessPolicy<TRequest>
{
    Task<bool> CanExecuteAsync(TRequest request, CancellationToken ct);
}
