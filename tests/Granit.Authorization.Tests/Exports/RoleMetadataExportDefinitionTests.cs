using Granit.Authorization.Exports;
using Granit.DataExchange.Export;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests.Exports;

public sealed class RoleMetadataExportDefinitionTests
{
    private static readonly RoleMetadataExportDefinition Sut = new();
    private static IReadOnlyList<ExportFieldDescriptor> Fields =>
        Sut.GetFields();

    [Fact]
    public void Name_is_stable() =>
        Sut.Name.ShouldBe("Granit.Authorization.RoleMetadataExport");

    [Theory]
    [InlineData("IsOrphaned")]
    [InlineData("OrphanedAt")]
    public void Orphan_fields_are_exported(string fieldName) =>
        Fields.ShouldContain(f => f.PropertyPath == fieldName);

    [Fact]
    public void IsOrphaned_and_OrphanedAt_are_adjacent()
    {
        int isOrphanedOrder = Fields.Single(f => f.PropertyPath == "IsOrphaned").Order;
        int orphanedAtOrder = Fields.Single(f => f.PropertyPath == "OrphanedAt").Order;
        orphanedAtOrder.ShouldBe(isOrphanedOrder + 1);
    }

    [Fact]
    public void HasComplexFields_is_false() =>
        ((IExportDefinitionDescriptor)Sut).HasComplexFields.ShouldBeFalse();
}
