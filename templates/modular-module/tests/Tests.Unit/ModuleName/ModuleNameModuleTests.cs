using Module.ModuleName.Shared;
using Shouldly;

namespace Tests.Unit.ModuleName;

public sealed class ModuleNameModuleTests
{
    [Fact]
    public void Module_name_is_the_schema_name() =>
        new ModuleNameModule().Name.ShouldBe(ModuleNameDbContext.SchemaName);
}
