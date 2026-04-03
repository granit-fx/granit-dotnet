using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Tests;

public sealed class ReferenceDataBuilderTests
{
    [Fact]
    public void Add_registers_type_with_default_options()
    {
        ReferenceDataBuilder builder = new();

        builder.Add("Countries");

        builder.Registrations.ShouldHaveSingleItem();
        builder.Registrations[0].TypeName.ShouldBe("Countries");
        builder.Registrations[0].Options.TableName.ShouldBe("ref_data");
    }

    [Fact]
    public void Add_with_configure_applies_options()
    {
        ReferenceDataBuilder builder = new();

        builder.Add("Countries", opts => opts.Table("ref_countries").Hierarchical());

        builder.Registrations.ShouldHaveSingleItem();
        builder.Registrations[0].Options.TableName.ShouldBe("ref_countries");
        builder.Registrations[0].Options.IsHierarchical.ShouldBeTrue();
    }

    [Fact]
    public void Add_returns_builder_for_chaining()
    {
        ReferenceDataBuilder builder = new();

        ReferenceDataBuilder result = builder
            .Add("Countries")
            .Add("Currencies");

        result.ShouldBeSameAs(builder);
        builder.Registrations.Count.ShouldBe(2);
    }

    [Fact]
    public void Add_null_typeName_throws()
    {
        ReferenceDataBuilder builder = new();

        Should.Throw<ArgumentException>(() => builder.Add(null!));
    }

    [Fact]
    public void Add_empty_typeName_throws()
    {
        ReferenceDataBuilder builder = new();

        Should.Throw<ArgumentException>(() => builder.Add(""));
    }

    [Fact]
    public void Add_whitespace_typeName_throws()
    {
        ReferenceDataBuilder builder = new();

        Should.Throw<ArgumentException>(() => builder.Add("   "));
    }
}
