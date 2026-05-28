using Granit.DataExchange.Import;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Mapping;

public sealed class ImportDefinitionBuilderTests
{
    [Fact]
    public void Property_registers_property_with_defaults()
    {
        // Arrange
        ImportDefinitionBuilder<TestEntity> builder = new();

        // Act
        builder.Property(e => e.Name);

        // Assert
        builder.Properties.Count.ShouldBe(1);
        builder.Properties[0].PropertyPath.ShouldBe("Name");
        builder.Properties[0].ClrTypeName.ShouldBe("String");
        builder.Properties[0].DisplayName.ShouldBeNull();
        builder.Properties[0].IsRequired.ShouldBeFalse();
        builder.Properties[0].Aliases.ShouldBeEmpty();
    }

    [Fact]
    public void Property_applies_fluent_configuration()
    {
        // Arrange
        ImportDefinitionBuilder<TestEntity> builder = new();

        // Act
        builder.Property(e => e.Email, p => p
            .DisplayName("Courriel")
            .Description("Adresse email")
            .Aliases("Mail", "E-mail")
            .Required()
            .Format("email"));

        // Assert
        PropertyMapping mapping = builder.Properties[0];
        mapping.PropertyPath.ShouldBe("Email");
        mapping.DisplayName.ShouldBe("Courriel");
        mapping.Description.ShouldBe("Adresse email");
        mapping.Aliases.ShouldBe(["Mail", "E-mail"]);
        mapping.IsRequired.ShouldBeTrue();
        mapping.Format.ShouldBe("email");
    }

    [Fact]
    public void HasBusinessKey_registers_single_key()
    {
        // Arrange
        ImportDefinitionBuilder<TestEntity> builder = new();

        // Act
        builder.HasBusinessKey(e => e.Niss);

        // Assert
        builder.BusinessKeyProperties.ShouldBe(["Niss"]);
    }

    [Fact]
    public void HasCompositeKey_registers_multiple_keys()
    {
        // Arrange
        ImportDefinitionBuilder<TestEntity> builder = new();

        // Act
        builder.HasCompositeKey(e => e.Name, e => e.BirthDate);

        // Assert
        builder.BusinessKeyProperties.ShouldBe(["Name", "BirthDate"]);
    }

    [Fact]
    public void ExcludeOnUpdate_registers_excluded_property()
    {
        // Arrange
        ImportDefinitionBuilder<TestEntity> builder = new();

        // Act
        builder.ExcludeOnUpdate(e => e.CreatedAt);

        // Assert
        builder.ExcludedOnUpdateProperties.ShouldBe(["CreatedAt"]);
    }

    [Fact]
    public void HasExternalId_sets_flag()
    {
        // Arrange
        ImportDefinitionBuilder<TestEntity> builder = new();

        // Act
        builder.HasExternalId();

        // Assert
        builder.HasExternalIdFlag.ShouldBeTrue();
    }

    [Fact]
    public void GroupBy_sets_column_name()
    {
        // Arrange
        ImportDefinitionBuilder<TestEntity> builder = new();

        // Act
        builder.GroupBy("OrderNumber");

        // Assert
        builder.GroupByColumn.ShouldBe("OrderNumber");
    }

    [Fact]
    public void HasMany_registers_child_properties()
    {
        // Arrange
        ImportDefinitionBuilder<TestParentEntity> builder = new();

        // Act
        builder.HasMany(e => e.Lines, child =>
        {
            child.Property(l => l.ProductName, p => p.DisplayName("Produit"));
            child.Property(l => l.Quantity, p => p.DisplayName("Quantité"));
        });

        // Assert
        builder.Properties.Count.ShouldBe(2);
        builder.Properties[0].PropertyPath.ShouldBe("Lines.ProductName");
        builder.Properties[0].DisplayName.ShouldBe("Produit");
        builder.Properties[0].IsChildCollection.ShouldBeTrue();
        builder.Properties[1].PropertyPath.ShouldBe("Lines.Quantity");
    }

    [Fact]
    public void Property_with_invalid_expression_throws()
    {
        // Arrange
        ImportDefinitionBuilder<TestEntity> builder = new();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.Property(e => e.Name.Length));
    }

    [Fact]
    public void Fluent_chaining_returns_same_builder()
    {
        // Arrange
        ImportDefinitionBuilder<TestEntity> builder = new();

        // Act
        ImportDefinitionBuilder<TestEntity> result = builder
            .HasBusinessKey(e => e.Niss)
            .Property(e => e.Name)
            .Property(e => e.Email)
            .ExcludeOnUpdate(e => e.CreatedAt);

        // Assert
        result.ShouldBeSameAs(builder);
    }
}

public sealed class TestEntity
{
    public string Niss { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset BirthDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class TestParentEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public ICollection<TestLineEntity> Lines { get; set; } = [];
}

public sealed class TestLineEntity
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
