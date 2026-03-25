using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests;

public sealed class ColumnBuilderTests
{
    [Fact]
    public void Label_sets_value()
    {
        ColumnBuilder<TestEntity> builder = new();

        builder.Label("Nom complet");

        builder.LabelValue.ShouldBe("Nom complet");
    }

    [Fact]
    public void Order_sets_value()
    {
        ColumnBuilder<TestEntity> builder = new();

        builder.Order(5);

        builder.OrderValue.ShouldBe(5);
    }

    [Fact]
    public void Sortable_defaults_to_true()
    {
        ColumnBuilder<TestEntity> builder = new();

        builder.Sortable();

        builder.IsSortableValue.ShouldBeTrue();
    }

    [Fact]
    public void Sortable_explicit_false()
    {
        ColumnBuilder<TestEntity> builder = new();

        builder.Sortable(false);

        builder.IsSortableValue.ShouldBeFalse();
    }

    [Fact]
    public void Filterable_defaults_to_true()
    {
        ColumnBuilder<TestEntity> builder = new();

        builder.Filterable();

        builder.IsFilterableValue.ShouldBeTrue();
    }

    [Fact]
    public void Visible_defaults_to_true_when_called()
    {
        ColumnBuilder<TestEntity> builder = new();

        builder.Visible();

        builder.IsVisibleValue.ShouldBeTrue();
    }

    [Fact]
    public void Visible_can_be_set_to_false()
    {
        ColumnBuilder<TestEntity> builder = new();

        builder.Visible(false);

        builder.IsVisibleValue.ShouldBeFalse();
    }

    [Fact]
    public void Default_IsVisible_is_true()
    {
        ColumnBuilder<TestEntity> builder = new();

        builder.IsVisibleValue.ShouldBeTrue();
    }

    [Fact]
    public void Format_sets_value()
    {
        ColumnBuilder<TestEntity> builder = new();

        builder.Format("dd/MM/yyyy");

        builder.FormatValue.ShouldBe("dd/MM/yyyy");
    }

    [Fact]
    public void Fluent_chaining_returns_same_builder()
    {
        ColumnBuilder<TestEntity> builder = new();

        ColumnBuilder<TestEntity> result = builder
            .Label("Test")
            .Order(1)
            .Sortable()
            .Filterable()
            .Visible()
            .Format("N2");

        result.ShouldBeSameAs(builder);
    }
}
