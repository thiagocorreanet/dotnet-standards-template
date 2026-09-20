using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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
    /// envolvidos pelo decorator de telemetria do módulo.
    /// </summary>
    public static IServiceCollection AddUseCasesFromAssembly(this IServiceCollection services, Assembly assembly, ModuleTelemetry telemetry)
    {
        var contextType = assembly.GetTypes().Single(t => !t.IsAbstract && typeof(DbContext).IsAssignableFrom(t));
        foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IUseCase<,>)))
            {
                var decorator = typeof(TelemetryUseCaseDecorator<,>).MakeGenericType(iface.GenericTypeArguments);
                services.AddScoped(type);
                services.AddScoped(iface, sp =>
                {
                    var command = type.GetCustomAttribute<CommandAttribute>();
                    var inner = command is null ? AuthorizedExecution.Create(sp, type, iface)
                        : ActivatorUtilities.CreateInstance(sp,
                            typeof(TransactionalUseCaseDecorator<,>).MakeGenericType(iface.GenericTypeArguments),
                            type, contextType, command);
                    return ActivatorUtilities.CreateInstance(sp, decorator, inner, telemetry);
                });
            }
        }

        return services;
    }
}
