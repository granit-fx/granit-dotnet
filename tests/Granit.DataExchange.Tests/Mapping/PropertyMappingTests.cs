using Granit.DataExchange.Import;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Mapping;

public sealed class PropertyMappingTests
{
    [Fact]
    public void ToFieldMetadata_maps_all_fields()
    {
        // Arrange
        PropertyMapping mapping = new()
        {
            PropertyPath = "Email",
            ClrTypeName = "String",
            DisplayName = "Courriel",
            Description = "Adresse email du patient",
            IsRequired = true,
            Format = "email",
            IsChildCollection = false,
        };

        // Act
        ImportFieldMetadata metadata = mapping.ToFieldMetadata();

        // Assert
        metadata.PropertyPath.ShouldBe("Email");
        metadata.ClrTypeName.ShouldBe("String");
        metadata.DisplayName.ShouldBe("Courriel");
        metadata.Description.ShouldBe("Adresse email du patient");
        metadata.IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void ToFieldMetadata_handles_null_optional_fields()
    {
        // Arrange
        PropertyMapping mapping = new()
        {
            PropertyPath = "Name",
            ClrTypeName = "String",
        };

        // Act
        ImportFieldMetadata metadata = mapping.ToFieldMetadata();

        // Assert
        metadata.DisplayName.ShouldBeNull();
        metadata.Description.ShouldBeNull();
        metadata.IsRequired.ShouldBeFalse();
    }

    [Fact]
    public void Default_aliases_is_empty()
    {
        // Arrange & Act
        PropertyMapping mapping = new()
        {
            PropertyPath = "Name",
            ClrTypeName = "String",
        };

        // Assert
        mapping.Aliases.ShouldBeEmpty();
    }

    [Fact]
    public void IsChildCollection_defaults_to_false()
    {
        // Arrange & Act
        PropertyMapping mapping = new()
        {
            PropertyPath = "Name",
            ClrTypeName = "String",
        };

        // Assert
        mapping.IsChildCollection.ShouldBeFalse();
    }
}
