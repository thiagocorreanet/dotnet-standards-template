using System.Reflection;
namespace Shared.Http.Endpoints;
/// <summary>Autorização de recurso fica no módulo; roles no endpoint são somente uma barreira adicional.</summary>
public interface IModuleAccessPolicy
{
    Assembly ModuleAssembly { get; }
    Task<bool> CanExecuteAsync(object request, CancellationToken ct);
}
