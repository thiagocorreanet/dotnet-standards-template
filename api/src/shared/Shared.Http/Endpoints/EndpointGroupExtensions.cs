using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Observability.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Shared.Http.Endpoints;

public static class EndpointGroupExtensions
{
    /// <summary>Cria o grupo padrão do módulo: prefixo <c>api/v1/{route}</c>, uma única tag OpenAPI, autenticação exigida por padrão e respostas comuns documentadas.</summary>
    public static RouteGroupBuilder MapModuleGroup(this IEndpointRouteBuilder endpoints, string route, string tag)
    {
        return endpoints.MapGroup($"api/v1/{route}")
            .WithTags(tag)
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    /// <summary>Mapeia todos os <see cref="IEndpoint"/> do assembly no grupo informado.</summary>
    public static RouteGroupBuilder MapEndpointsFromAssembly(this RouteGroupBuilder group, Assembly assembly)
    {
        var types = assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IEndpoint).IsAssignableFrom(t));
        foreach (var type in types)
        {
            var map = type.GetMethod(nameof(IEndpoint.Map), BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException($"{type.Name} não implementa Map estático.");
            map.Invoke(null, [group]);
        }

        return group;
    }

    /// <summary>
    /// Registra todos os casos de uso (<see cref="IUseCase{TRequest,TResponse}"/>) do assembly como scoped,
    /// envolvidos pelo decorator de telemetria do módulo, e a <see cref="IAccessPolicy{TRequest}"/> de cada um.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Caso de uso sem policy, ou com mais de uma, ou <see cref="CommandAttribute"/> com placeholder inválido, interrompe a
    /// composição: falha no startup, não na primeira requisição.
    /// </exception>
    public static IServiceCollection AddUseCasesFromAssembly(this IServiceCollection services, Assembly assembly, ModuleTelemetry telemetry) =>
        services.AddUseCasesFromTypes(assembly.GetTypes(), telemetry);

    internal static IServiceCollection AddUseCasesFromTypes(this IServiceCollection services, Type[] types, ModuleTelemetry telemetry)
    {
        var contextType = types.Single(t => !t.IsAbstract && typeof(DbContext).IsAssignableFrom(t));
        foreach (var type in types.Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IUseCase<,>)))
            {
                var policyContract = typeof(IAccessPolicy<>).MakeGenericType(iface.GenericTypeArguments[0]);
                services.TryAddScoped(policyContract, FindAccessPolicy(types, type, policyContract));
                var decorator = typeof(TelemetryUseCaseDecorator<,>).MakeGenericType(iface.GenericTypeArguments);
                var command = type.GetCustomAttribute<CommandAttribute>();
                var consistencyKey = command is null ? null : ConsistencyKey.For(type, iface.GenericTypeArguments[0], command, telemetry.Module);
                services.AddScoped(type);
                services.AddScoped(iface, sp =>
                {
                    var inner = consistencyKey is null ? AuthorizedExecution.Create(sp, type, iface)
                        : ActivatorUtilities.CreateInstance(sp,
                            typeof(TransactionalUseCaseDecorator<,>).MakeGenericType(iface.GenericTypeArguments),
                            type, contextType, consistencyKey);
                    return ActivatorUtilities.CreateInstance(sp, decorator, inner, telemetry);
                });
            }
        }

        return services;
    }

    private static Type FindAccessPolicy(Type[] types, Type useCaseType, Type policyContract)
    {
        var candidates = types.Where(t => t is { IsClass: true, IsAbstract: false } && policyContract.IsAssignableFrom(t)).ToArray();
        var request = policyContract.GenericTypeArguments[0].Name;
        return candidates.Length switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException(
                $"Caso de uso {useCaseType.Name} sem política de acesso. Crie uma classe que implemente IAccessPolicy<{request}> no diretório do caso de uso."),
            _ => throw new InvalidOperationException(
                $"Caso de uso {useCaseType.Name} com mais de uma IAccessPolicy<{request}>: {string.Join(", ", candidates.Select(c => c.Name))}."),
        };
    }
}
