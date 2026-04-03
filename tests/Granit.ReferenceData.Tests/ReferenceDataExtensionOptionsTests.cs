using Granit.ReferenceData.Options;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Tests;

public sealed class ReferenceDataExtensionOptionsTests
{
    [Fact]
    public void Default_TableName_is_ref_data()
    {
        ReferenceDataExtensionOptions options = new();

        options.TableName.ShouldBe("ref_data");
    }

    [Fact]
    public void Default_IsHierarchical_is_false()
    {
        ReferenceDataExtensionOptions options = new();

        options.IsHierarchical.ShouldBeFalse();
    }

    [Fact]
    public void Default_PropertyMappings_is_empty()
    {
        ReferenceDataExtensionOptions options = new();

        options.PropertyMappings.ShouldBeEmpty();
    }

    [Fact]
    public void Table_sets_TableName_and_returns_self()
    {
        ReferenceDataExtensionOptions options = new();

        ReferenceDataExtensionOptions result = options.Table("ref_countries");

        result.ShouldBeSameAs(options);
        options.TableName.ShouldBe("ref_countries");
    }

    [Fact]
    public void Table_null_throws()
    {
        ReferenceDataExtensionOptions options = new();

        Should.Throw<ArgumentException>(() => options.Table(null!));
    }

    [Fact]
    public void Table_empty_throws()
    {
        ReferenceDataExtensionOptions options = new();

        Should.Throw<ArgumentException>(() => options.Table(""));
    }

    [Fact]
    public void Hierarchical_sets_IsHierarchical_and_returns_self()
    {
        ReferenceDataExtensionOptions options = new();

        ReferenceDataExtensionOptions result = options.Hierarchical();

        result.ShouldBeSameAs(options);
        options.IsHierarchical.ShouldBeTrue();
    }

    [Fact]
    public void MapProperty_adds_mapping_with_all_parameters()
    {
        ReferenceDataExtensionOptions options = new();

        ReferenceDataExtensionOptions result = options.MapProperty<string>(
            "Alpha3Code",
            maxLength: 3,
            isRequired: true,
            isFilterable: true,
            isSortable: true);

        result.ShouldBeSameAs(options);
        options.PropertyMappings.ShouldHaveSingleItem();

        ReferenceDataPropertyMapping mapping = options.PropertyMappings[0];
        mapping.Name.ShouldBe("Alpha3Code");
        mapping.ClrType.ShouldBe(typeof(string));
        mapping.MaxLength.ShouldBe(3);
        mapping.IsRequired.ShouldBeTrue();
        mapping.IsFilterable.ShouldBeTrue();
        mapping.IsSortable.ShouldBeTrue();
    }

    [Fact]
    public void MapProperty_defaults_to_optional_non_filterable_non_sortable()
    {
        ReferenceDataExtensionOptions options = new();

        options.MapProperty<int>("Population");

        ReferenceDataPropertyMapping mapping = options.PropertyMappings[0];
        mapping.ClrType.ShouldBe(typeof(int));
        mapping.MaxLength.ShouldBeNull();
        mapping.IsRequired.ShouldBeFalse();
        mapping.IsFilterable.ShouldBeFalse();
        mapping.IsSortable.ShouldBeFalse();
    }

    [Fact]
    public void MapProperty_null_name_throws()
    {
        ReferenceDataExtensionOptions options = new();

        Should.Throw<ArgumentException>(() => options.MapProperty<string>(null!));
    }

    [Fact]
    public void MapProperty_multiple_adds_all()
    {
        ReferenceDataExtensionOptions options = new();

        options
            .MapProperty<string>("Alpha3Code", maxLength: 3)
            .MapProperty<int>("Population")
            .MapProperty<bool>("IsMember");

        options.PropertyMappings.Count.ShouldBe(3);
    }

    [Fact]
    public void Fluent_chain_combines_all_options()
    {
        ReferenceDataExtensionOptions options = new();

        options
            .Table("ref_countries")
            .Hierarchical()
            .MapProperty<string>("Alpha3Code", maxLength: 3, isFilterable: true);

        options.TableName.ShouldBe("ref_countries");
        options.IsHierarchical.ShouldBeTrue();
        options.PropertyMappings.ShouldHaveSingleItem();
    }
}
