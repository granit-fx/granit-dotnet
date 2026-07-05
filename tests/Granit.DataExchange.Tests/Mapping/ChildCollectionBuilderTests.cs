using Granit.DataExchange.Import;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Mapping;

public sealed class ChildCollectionBuilderTests
{
    [Fact]
    public void Property_registers_child_property_with_dotted_path()
    {
        // Arrange
        ChildCollectionBuilder<TestLineEntity> builder = new("Lines");

        // Act
        builder.Property(l => l.ProductName);

        // Assert
        builder.Properties.Count.ShouldBe(1);
        builder.Properties[0].PropertyPath.ShouldBe("Lines.ProductName");
        builder.Properties[0].ClrTypeName.ShouldBe("String");
        builder.Properties[0].IsChildCollection.ShouldBeTrue();
    }

    [Fact]
    public void Property_applies_fluent_configuration()
    {
        // Arrange
        ChildCollectionBuilder<TestLineEntity> builder = new("Lines");

        // Act
        builder.Property(l => l.ProductName, p => p
            .DisplayName("Produit")
            .Description("Nom du produit")
            .Aliases("Product", "Article")
            .Required()
            .Format("text"));

        // Assert
        PropertyMapping mapping = builder.Properties[0];
        mapping.DisplayName.ShouldBe("Produit");
        mapping.Description.ShouldBe("Nom du produit");
        mapping.Aliases.ShouldBe(["Product", "Article"]);
        mapping.IsRequired.ShouldBeTrue();
        mapping.Format.ShouldBe("text");
    }

    [Fact]
    public void Property_without_configure_uses_defaults()
    {
        // Arrange
        ChildCollectionBuilder<TestLineEntity> builder = new("Lines");

        // Act
        builder.Property(l => l.Quantity);

        // Assert
        PropertyMapping mapping = builder.Properties[0];
        mapping.PropertyPath.ShouldBe("Lines.Quantity");
        mapping.ClrTypeName.ShouldBe("Int32");
        mapping.DisplayName.ShouldBeNull();
        mapping.Description.ShouldBeNull();
        mapping.Aliases.ShouldBeEmpty();
        mapping.IsRequired.ShouldBeFalse();
        mapping.Format.ShouldBeNull();
        mapping.IsChildCollection.ShouldBeTrue();
    }

    [Fact]
    public void Property_returns_same_builder_for_chaining()
    {
        // Arrange
        ChildCollectionBuilder<TestLineEntity> builder = new("Lines");

        // Act
        ChildCollectionBuilder<TestLineEntity> result = builder
            .Property(l => l.ProductName)
            .Property(l => l.Quantity);

        // Assert
        result.ShouldBeSameAs(builder);
        builder.Properties.Count.ShouldBe(2);
    }

    [Fact]
    public void Property_with_invalid_expression_throws()
    {
        // Arrange
        ChildCollectionBuilder<TestLineEntity> builder = new("Lines");

        // Act & Assert — method call is not a MemberExpression.
        // Uses Quantity.ToString() (int -> string), a non-redundant call RCS1097 leaves intact,
        // so the analyzer can't rewrite it back to a bare member access (see #2932 regression).
        Should.Throw<ArgumentException>(() =>
            builder.Property(l => l.Quantity.ToString()));
    }

    [Fact]
    public void Multiple_properties_registered_in_order()
    {
        // Arrange
        ChildCollectionBuilder<TestLineEntity> builder = new("OrderLines");

        // Act
        builder
            .Property(l => l.ProductName, p => p.DisplayName("Produit"))
            .Property(l => l.Quantity, p => p.DisplayName("Quantité"));

        // Assert
        builder.Properties.Count.ShouldBe(2);
        builder.Properties[0].PropertyPath.ShouldBe("OrderLines.ProductName");
        builder.Properties[1].PropertyPath.ShouldBe("OrderLines.Quantity");
    }
}
