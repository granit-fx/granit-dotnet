using Granit.DataExchange.Export;
using Granit.Timeline.Exports;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests.Exports;

public sealed class TimelineEntryExportDefinitionTests
{
    private static readonly TimelineEntryExportDefinition Sut = new();
    private static IReadOnlyList<ExportFieldDescriptor> Fields =>
        Sut.GetFields();

    [Fact]
    public void Name_is_stable() =>
        Sut.Name.ShouldBe("Granit.Timeline.TimelineEntryExport");

    [Theory]
    [InlineData("AuthorName")]
    [InlineData("SourceKey")]
    [InlineData("SourceId")]
    public void New_scalar_fields_are_exported(string fieldName) =>
        Fields.ShouldContain(f => f.PropertyPath == fieldName);

    [Fact]
    public void AuthorName_follows_AuthorId()
    {
        int authorIdOrder = Fields.Single(f => f.PropertyPath == "AuthorId").Order;
        int authorNameOrder = Fields.Single(f => f.PropertyPath == "AuthorName").Order;
        authorNameOrder.ShouldBe(authorIdOrder + 1);
    }

    [Fact]
    public void SourceKey_and_SourceId_are_adjacent()
    {
        int keyOrder = Fields.Single(f => f.PropertyPath == "SourceKey").Order;
        int idOrder = Fields.Single(f => f.PropertyPath == "SourceId").Order;
        idOrder.ShouldBe(keyOrder + 1);
    }

    [Fact]
    public void HasComplexFields_is_false() =>
        ((IExportDefinitionDescriptor)Sut).HasComplexFields.ShouldBeFalse();
}
