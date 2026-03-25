using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests;

public sealed class QueryDefinitionBuilderTests
{
    [Fact]
    public void Column_registers_property_with_defaults()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.Column(e => e.Name);

        builder.Columns.Count.ShouldBe(1);
        builder.Columns[0].PropertyName.ShouldBe("Name");
        builder.Columns[0].ClrType.ShouldBe(typeof(string));
        builder.Columns[0].Label.ShouldBeNull();
        builder.Columns[0].IsSortable.ShouldBeFalse();
        builder.Columns[0].IsFilterable.ShouldBeFalse();
        builder.Columns[0].IsVisible.ShouldBeTrue();
        builder.Columns[0].Format.ShouldBeNull();
    }

    [Fact]
    public void Column_applies_fluent_configuration()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.Column(e => e.Email, c => c
            .Label("Courriel")
            .Order(2)
            .Sortable()
            .Filterable()
            .Visible(false)
            .Format("email"));

        ColumnDescriptor column = builder.Columns[0];
        column.PropertyName.ShouldBe("Email");
        column.ClrType.ShouldBe(typeof(string));
        column.Label.ShouldBe("Courriel");
        column.Order.ShouldBe(2);
        column.IsSortable.ShouldBeTrue();
        column.IsFilterable.ShouldBeTrue();
        column.IsVisible.ShouldBeFalse();
        column.Format.ShouldBe("email");
    }

    [Fact]
    public void GlobalSearch_registers_properties()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.GlobalSearch(e => e.Name, e => e.Email);

        builder.GlobalSearchProperties.ShouldBe(["Name", "Email"]);
    }

    [Fact]
    public void DefaultPageSize_sets_value()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.DefaultPageSize(50);

        builder.DefaultPageSizeValue.ShouldBe(50);
    }

    [Fact]
    public void DefaultPageSize_default_is_20()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.DefaultPageSizeValue.ShouldBe(20);
    }

    [Fact]
    public void DefaultPageSize_throws_for_zero()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            builder.DefaultPageSize(0));
    }

    [Fact]
    public void DefaultPageSize_throws_for_negative()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            builder.DefaultPageSize(-1));
    }

    [Fact]
    public void MaxPageSize_sets_value()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.MaxPageSize(500);

        builder.MaxPageSizeValue.ShouldBe(500);
    }

    [Fact]
    public void MaxPageSize_default_is_100()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.MaxPageSizeValue.ShouldBe(100);
    }

    [Fact]
    public void MaxPageSize_throws_for_zero()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            builder.MaxPageSize(0));
    }

    [Fact]
    public void SupportsCursorPagination_sets_property_name()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.SupportsCursorPagination(e => e.Id);

        builder.CursorPropertyName.ShouldBe("Id");
    }

    [Fact]
    public void DefaultSort_sets_value()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.DefaultSort("-createdAt,lastName");

        builder.DefaultSortValue.ShouldBe("-createdAt,lastName");
    }

    [Fact]
    public void Column_with_invalid_expression_throws()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        Should.Throw<ArgumentException>(() =>
            builder.Column(e => e.Name.Length));
    }

    [Fact]
    public void Fluent_chaining_returns_same_builder()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        QueryDefinitionBuilder<TestEntity> result = builder
            .Column(e => e.Name, c => c.Sortable())
            .Column(e => e.Email, c => c.Filterable())
            .GlobalSearch(e => e.Name)
            .DefaultPageSize(25)
            .MaxPageSize(200)
            .DefaultSort("-createdAt");

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Multiple_columns_are_registered_in_order()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder
            .Column(e => e.Name, c => c.Order(1))
            .Column(e => e.Email, c => c.Order(2))
            .Column(e => e.Age, c => c.Order(3));

        builder.Columns.Count.ShouldBe(3);
        builder.Columns[0].PropertyName.ShouldBe("Name");
        builder.Columns[1].PropertyName.ShouldBe("Email");
        builder.Columns[2].PropertyName.ShouldBe("Age");
    }

    [Fact]
    public void Column_with_nullable_property_records_correct_type()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.Column(e => e.BirthDate);

        builder.Columns[0].ClrType.ShouldBe(typeof(DateTimeOffset?));
    }

    [Fact]
    public void QuickFilter_registers_filter_without_label()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.QuickFilter("MyItems", e => e.Name == "test");

        builder.QuickFilters.Count.ShouldBe(1);
        builder.QuickFilters[0].Name.ShouldBe("MyItems");
        builder.QuickFilters[0].Label.ShouldBeNull();
        builder.QuickFilters[0].IsDefault.ShouldBeFalse();
        builder.QuickFilters[0].Predicate.ShouldNotBeNull();
    }

    [Fact]
    public void QuickFilter_registers_filter_with_label_and_default()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.QuickFilter("MyItems", "Mes éléments", e => e.Name == "test", isDefault: true);

        builder.QuickFilters.Count.ShouldBe(1);
        builder.QuickFilters[0].Name.ShouldBe("MyItems");
        builder.QuickFilters[0].Label.ShouldBe("Mes éléments");
        builder.QuickFilters[0].IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void QuickFilter_chaining_returns_same_builder()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        QueryDefinitionBuilder<TestEntity> result = builder
            .QuickFilter("A", e => e.Age > 18)
            .QuickFilter("B", "Label B", e => e.Age < 65);

        result.ShouldBeSameAs(builder);
        builder.QuickFilters.Count.ShouldBe(2);
    }
}

public sealed class TestEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTimeOffset? BirthDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
