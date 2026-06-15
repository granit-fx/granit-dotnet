using Granit.Hostnames.Domain;
using Granit.Hostnames.Queries;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.Tests;

// Regression for issue #2767: the definition previously used `e => e.Host.Value` (drilling into
// the Hostname value object), which threw ArgumentException at construction — the grid was
// uninstantiable and untested. Host is now a display/sort-only column (value objects cannot be
// substring-searched or filtered), and global search is on the plain-string OwnerType.
public sealed class ManagedHostnameQueryDefinitionTests
{
    [Fact]
    public void Definition_constructs_without_throwing()
    {
        ManagedHostnameQueryDefinition definition = new();

        Should.NotThrow(() => definition.GetColumns());
    }

    [Fact]
    public void Host_column_is_sortable_but_not_filterable()
    {
        ManagedHostnameQueryDefinition definition = new();

        ColumnDescriptor host = definition.GetColumns()
            .Single(c => c.PropertyName == nameof(ManagedHostname.Host));

        host.IsSortable.ShouldBeTrue();
        host.IsFilterable.ShouldBeFalse();
    }

    [Fact]
    public void GlobalSearch_targets_OwnerType_not_the_Host_value_object()
    {
        ManagedHostnameQueryDefinition definition = new();

        IReadOnlyList<string> search = definition.GetGlobalSearchProperties();

        search.ShouldContain(nameof(ManagedHostname.OwnerType));
        search.ShouldNotContain(nameof(ManagedHostname.Host));
        search.ShouldNotContain("Value");
    }
}
