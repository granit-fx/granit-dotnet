using Granit.DataExchange.Export;
using Granit.Scheduling.Exports;
using Shouldly;
using Xunit;

namespace Granit.Scheduling.Tests.Exports;

public sealed class ScheduledActionExportDefinitionTests
{
    private static readonly ScheduledActionExportDefinition Sut = new();
    private static IReadOnlyList<ExportFieldDescriptor> Fields =>
        ((IExportDefinitionDescriptor)Sut).GetFields();

    [Fact]
    public void Name_is_stable() =>
        Sut.Name.ShouldBe("Granit.Scheduling.ScheduledActionExport");

    [Fact]
    public void PayloadJson_is_exported_as_scalar()
    {
        ExportFieldDescriptor field = Fields.Single(f => f.PropertyPath == "PayloadJson");
        field.RequiresHierarchy.ShouldBeFalse();
        field.ValueSelector.ShouldBeNull();
    }

    [Fact]
    public void PayloadType_and_PayloadJson_are_adjacent()
    {
        // PayloadJson must immediately follow PayloadType so the pair is co-located
        // when consumers iterate fields for deserialization.
        int typeOrder = Fields.Single(f => f.PropertyPath == "PayloadType").Order;
        int jsonOrder = Fields.Single(f => f.PropertyPath == "PayloadJson").Order;
        jsonOrder.ShouldBe(typeOrder + 1);
    }

    [Fact]
    public void HasComplexFields_is_false() =>
        ((IExportDefinitionDescriptor)Sut).HasComplexFields.ShouldBeFalse();
}
