using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Results;
namespace Shared.Http.Endpoints;
internal sealed class AuthorizedUseCaseDecorator<TRequest, TResponse>(IUseCase<TRequest, TResponse> inner, IAccessPolicy<TRequest> policy)
    : IUseCase<TRequest, TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken ct)
    {
        if (request is null || !await policy.CanExecuteAsync(request, ct))
            return Error.Forbidden("Authorization.ResourceDenied", "Acesso ao recurso não autorizado.");
        return await inner.HandleAsync(request, ct);
    }
}
internal static class AuthorizedExecution
{
    public static object Create(IServiceProvider provider, Type useCaseType, Type contractType)
    {
        var requestType = contractType.GenericTypeArguments[0];
        var policy = provider.GetRequiredService(typeof(IAccessPolicy<>).MakeGenericType(requestType));
        return ActivatorUtilities.CreateInstance(provider, typeof(AuthorizedUseCaseDecorator<,>).MakeGenericType(contractType.GenericTypeArguments),
            provider.GetRequiredService(useCaseType), policy);
    }
}
