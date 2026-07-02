using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

public sealed class ModuleOfTests
{
    [Fact]
    public void Strips_the_Granit_prefix()
    {
        // ModuleOfTests lives in this test assembly (Granit.QueryEngine.Abstractions.Tests),
        // whose name carries no trailing layer suffix (".Tests" is not one), so only the
        // framework prefix is removed.
        IQueryDefinitionDescriptor.ModuleOf(typeof(ModuleOfTests))
            .ShouldBe("QueryEngine.Abstractions.Tests");
    }

    [Fact]
    public void Strips_a_trailing_layer_suffix_so_an_Abstractions_hosted_entity_groups_under_its_module()
    {
        // QueryRequest lives in Granit.QueryEngine.Abstractions — the .Abstractions layer
        // suffix is never the logical owning module, so it collapses to "QueryEngine"
        // (mirrors Granit.Auditing.Abstractions.AuditEntry → "Auditing").
        IQueryDefinitionDescriptor.ModuleOf(typeof(QueryRequest))
            .ShouldBe("QueryEngine");
    }

    [Fact]
    public void Leaves_a_non_framework_assembly_name_unchanged()
    {
        IQueryDefinitionDescriptor.ModuleOf(typeof(string))
            .ShouldBe(typeof(string).Assembly.GetName().Name);
    }
}
