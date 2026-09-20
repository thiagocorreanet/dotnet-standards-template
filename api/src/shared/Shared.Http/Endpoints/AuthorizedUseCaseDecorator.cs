using Microsoft.Extensions.DependencyInjection;
using Shared.Http.Results;
namespace Shared.Http.Endpoints;
internal sealed class AuthorizedUseCaseDecorator<TRequest, TResponse>(IUseCase<TRequest, TResponse> inner, IModuleAccessPolicy policy)
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
        var policy = provider.GetServices<IModuleAccessPolicy>().SingleOrDefault(p => p.ModuleAssembly == useCaseType.Assembly)
            ?? throw new InvalidOperationException($"Política de recurso ausente: {useCaseType.Assembly.GetName().Name}");
        return ActivatorUtilities.CreateInstance(provider, typeof(AuthorizedUseCaseDecorator<,>).MakeGenericType(contractType.GenericTypeArguments),
            provider.GetRequiredService(useCaseType), policy);
    }
}
