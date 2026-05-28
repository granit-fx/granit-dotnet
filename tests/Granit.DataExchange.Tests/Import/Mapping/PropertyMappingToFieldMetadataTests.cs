using Granit.DataExchange.Import;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Mapping;

public sealed class PropertyMappingToFieldMetadataTests
{
    [Fact]
    public void ToFieldMetadata_MapsAllProperties()
    {
        PropertyMapping mapping = new()
        {
            PropertyPath = "Email",
            ClrTypeName = "String",
            DisplayName = "Email Address",
            Description = "The user's email",
            IsRequired = true,
            Aliases = ["Courriel", "Mail"],
            Format = "email",
        };

        ImportFieldMetadata metadata = mapping.ToFieldMetadata();

        metadata.PropertyPath.ShouldBe("Email");
        metadata.ClrTypeName.ShouldBe("String");
        metadata.DisplayName.ShouldBe("Email Address");
        metadata.Description.ShouldBe("The user's email");
        metadata.IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void ToFieldMetadata_WithNullOptionalFields()
    {
        PropertyMapping mapping = new()
        {
            PropertyPath = "Name",
            ClrTypeName = "String",
            DisplayName = null,
            Description = null,
            IsRequired = false,
        };

        ImportFieldMetadata metadata = mapping.ToFieldMetadata();

        metadata.DisplayName.ShouldBeNull();
        metadata.Description.ShouldBeNull();
        metadata.IsRequired.ShouldBeFalse();
    }

    [Fact]
    public void PropertyMapping_IsChildCollection_DefaultIsFalse()
    {
        PropertyMapping mapping = new()
        {
            PropertyPath = "Name",
            ClrTypeName = "String",
        };

        mapping.IsChildCollection.ShouldBeFalse();
    }

    [Fact]
    public void PropertyMapping_Aliases_DefaultsToEmpty()
    {
        PropertyMapping mapping = new()
        {
            PropertyPath = "Name",
            ClrTypeName = "String",
        };

        mapping.Aliases.ShouldNotBeNull();
        mapping.Aliases.ShouldBeEmpty();
    }
}
