using Granit.DataExchange.Import;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Mapping;

public sealed class PropertyMappingBuilderTests
{
    [Fact]
    public void Default_values_are_null_or_empty()
    {
        // Arrange & Act
        PropertyMappingBuilder builder = new();

        // Assert
        builder.DisplayNameValue.ShouldBeNull();
        builder.DescriptionValue.ShouldBeNull();
        builder.AliasValues.ShouldBeEmpty();
        builder.IsRequired.ShouldBeFalse();
        builder.FormatValue.ShouldBeNull();
    }

    [Fact]
    public void DisplayName_sets_value_and_returns_builder()
    {
        // Arrange
        PropertyMappingBuilder builder = new();

        // Act
        PropertyMappingBuilder result = builder.DisplayName("Prénom");

        // Assert
        builder.DisplayNameValue.ShouldBe("Prénom");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Description_sets_value_and_returns_builder()
    {
        // Arrange
        PropertyMappingBuilder builder = new();

        // Act
        PropertyMappingBuilder result = builder.Description("Adresse email du patient");

        // Assert
        builder.DescriptionValue.ShouldBe("Adresse email du patient");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Aliases_adds_multiple_values()
    {
        // Arrange
        PropertyMappingBuilder builder = new();

        // Act
        builder.Aliases("Mail", "Courriel", "E-mail");

        // Assert
        builder.AliasValues.ShouldBe(["Mail", "Courriel", "E-mail"]);
    }

    [Fact]
    public void Aliases_accumulates_across_calls()
    {
        // Arrange
        PropertyMappingBuilder builder = new();

        // Act
        builder.Aliases("Mail").Aliases("Courriel");

        // Assert
        builder.AliasValues.ShouldBe(["Mail", "Courriel"]);
    }

    [Fact]
    public void Required_sets_flag()
    {
        // Arrange
        PropertyMappingBuilder builder = new();

        // Act
        builder.Required();

        // Assert
        builder.IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void Required_false_clears_flag()
    {
        // Arrange
        PropertyMappingBuilder builder = new();
        builder.Required();

        // Act
        builder.Required(false);

        // Assert
        builder.IsRequired.ShouldBeFalse();
    }

    [Fact]
    public void Format_sets_value()
    {
        // Arrange
        PropertyMappingBuilder builder = new();

        // Act
        builder.Format("dd/MM/yyyy");

        // Assert
        builder.FormatValue.ShouldBe("dd/MM/yyyy");
    }

    [Fact]
    public void Full_fluent_chain_works()
    {
        // Arrange
        PropertyMappingBuilder builder = new();

        // Act
        PropertyMappingBuilder result = builder
            .DisplayName("Email")
            .Description("Email address")
            .Aliases("Courriel", "Mail")
            .Required()
            .Format("email");

        // Assert
        result.ShouldBeSameAs(builder);
        builder.DisplayNameValue.ShouldBe("Email");
        builder.DescriptionValue.ShouldBe("Email address");
        builder.AliasValues.Count.ShouldBe(2);
        builder.IsRequired.ShouldBeTrue();
        builder.FormatValue.ShouldBe("email");
    }
}
