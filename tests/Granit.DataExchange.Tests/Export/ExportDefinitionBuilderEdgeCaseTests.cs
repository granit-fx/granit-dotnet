using Granit.DataExchange.Export;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Export;

public sealed class ExportDefinitionBuilderEdgeCaseTests
{
    private sealed class TestEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public TestNav? Nav { get; set; }
    }

    private sealed class TestNav
    {
        public string Label { get; set; } = string.Empty;
    }

    [Fact]
    public void Field_InvalidExpression_ThrowsArgumentException()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        Should.Throw<ArgumentException>(() =>
            builder.Field(e => e.Name + "suffix"));
    }

    [Fact]
    public void Field_NavigationWithFormat_SetsFormatOnNavField()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.Field(e => e.Nav, n => n.Label, f => f.Format("upper").Header("Navigation Label").Order(99));

        ExportFieldDescriptor field = builder.Fields[0];
        field.PropertyPath.ShouldBe("Nav.Label");
        field.Format.ShouldBe("upper");
        field.Header.ShouldBe("Navigation Label");
        field.Order.ShouldBe(99);
        field.IsNavigation.ShouldBeTrue();
    }

    [Fact]
    public void Field_ExplicitOrderOverridesAutoOrder()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.Field(e => e.Name);             // auto-order = 0
        builder.Field(e => e.Value, f => f.Order(50));  // explicit = 50

        builder.Fields[0].Order.ShouldBe(0);
        builder.Fields[1].Order.ShouldBe(50);
    }

    [Fact]
    public void IncludeId_DefaultIsFalse()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.IncludeIdFlag.ShouldBeFalse();
    }

    [Fact]
    public void IncludeBusinessKey_DefaultIsFalse()
    {
        ExportDefinitionBuilder<TestEntity> builder = new();

        builder.IncludeBusinessKeyFlag.ShouldBeFalse();
    }
}
