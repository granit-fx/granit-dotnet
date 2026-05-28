using Granit.DataExchange.Import;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Mapping;

public sealed class ImportFieldMetadataTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        ImportFieldMetadata metadata = new(
            "Email", "String", "Email Address", "The user's email", true);

        metadata.PropertyPath.ShouldBe("Email");
        metadata.ClrTypeName.ShouldBe("String");
        metadata.DisplayName.ShouldBe("Email Address");
        metadata.Description.ShouldBe("The user's email");
        metadata.IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithNullOptionalFields()
    {
        ImportFieldMetadata metadata = new("Name", "String", null, null, false);

        metadata.DisplayName.ShouldBeNull();
        metadata.Description.ShouldBeNull();
        metadata.IsRequired.ShouldBeFalse();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        ImportFieldMetadata a = new("Email", "String", "Email", null, true);
        ImportFieldMetadata b = new("Email", "String", "Email", null, true);

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        ImportFieldMetadata a = new("Email", "String", "Email", null, true);
        ImportFieldMetadata b = new("Name", "String", "Name", null, false);

        a.ShouldNotBe(b);
    }
}
