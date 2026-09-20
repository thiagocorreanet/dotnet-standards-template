using System.Reflection;
using Shared.WebHost.Modules;
namespace Tests.Architecture;
public static class ArchitectureTestData
{
    public static readonly Assembly[] ModuleAssemblies = ModuleDiscovery.Discover().Select(m => m.GetType().Assembly).ToArray();
    public static IEnumerable<object[]> Modules => ModuleAssemblies.Select(a => new object[] { a });
}
