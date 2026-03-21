using Granit.Privacy.LegalAgreements;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.LegalAgreements;

public sealed class LegalDocumentDefinitionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        LegalDocumentDefinition sut = new("privacy-policy", "2.1.0", "Privacy Policy");

        sut.DocumentId.ShouldBe("privacy-policy");
        sut.CurrentVersion.ShouldBe("2.1.0");
        sut.DisplayName.ShouldBe("Privacy Policy");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        LegalDocumentDefinition a = new("privacy-policy", "1.0.0", "Privacy Policy");
        LegalDocumentDefinition b = new("privacy-policy", "1.0.0", "Privacy Policy");

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentDocumentId_AreNotEqual()
    {
        LegalDocumentDefinition a = new("privacy-policy", "1.0.0", "Privacy Policy");
        LegalDocumentDefinition b = new("terms", "1.0.0", "Privacy Policy");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_DifferentVersion_AreNotEqual()
    {
        LegalDocumentDefinition a = new("privacy-policy", "1.0.0", "Privacy Policy");
        LegalDocumentDefinition b = new("privacy-policy", "2.0.0", "Privacy Policy");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_DifferentDisplayName_AreNotEqual()
    {
        LegalDocumentDefinition a = new("privacy-policy", "1.0.0", "Privacy Policy");
        LegalDocumentDefinition b = new("privacy-policy", "1.0.0", "Updated Privacy Policy");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void ToString_ContainsDocumentId()
    {
        LegalDocumentDefinition sut = new("privacy-policy", "2.0.0", "Privacy Policy");

        string result = sut.ToString();

        result.ShouldContain("privacy-policy");
    }
}
