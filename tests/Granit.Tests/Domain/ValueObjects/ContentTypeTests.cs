using Granit.Domain;
using Granit.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain.ValueObjects;

public sealed class ContentTypeTests
{
    // -------------------------------------------------------------------------
    // Create — valid MIME types
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/json")]
    [InlineData("application/pdf")]
    [InlineData("image/png")]
    [InlineData("multipart/form-data")]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("application/octet-stream")]
    public void Create_ValidMimeType_Succeeds(string mimeType)
    {
        var result = ContentType.Create(mimeType);

        result.Value.ShouldBe(mimeType);
    }

    // -------------------------------------------------------------------------
    // Create — invalid MIME types
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_NullOrWhitespace_Throws()
    {
        Should.Throw<ArgumentException>(() => ContentType.Create(null!));
        Should.Throw<ArgumentException>(() => ContentType.Create(""));
        Should.Throw<ArgumentException>(() => ContentType.Create("   "));
    }

    [Theory]
    [InlineData("plaintext")]
    [InlineData("text")]
    [InlineData("/html")]
    [InlineData("text/")]
    public void Create_InvalidMimeFormat_Throws(string invalidType) => Should.Throw<ArgumentException>(() => ContentType.Create(invalidType));

    // -------------------------------------------------------------------------
    // Implicit conversions
    // -------------------------------------------------------------------------

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var contentType = ContentType.Create("application/json");

        string result = contentType;

        result.ShouldBe("application/json");
    }

    [Fact]
    public void ImplicitConversion_FromString_CreatesInstance()
    {
        ContentType contentType = "text/plain";

        contentType.Value.ShouldBe("text/plain");
    }

    // -------------------------------------------------------------------------
    // Equality — inherited from SingleValueObject
    // -------------------------------------------------------------------------

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = ContentType.Create("application/json");
        var b = ContentType.Create("application/json");

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        var a = ContentType.Create("application/json");
        var b = ContentType.Create("text/html");

        a.ShouldNotBe(b);
        (a != b).ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // ToString — inherited from SingleValueObject
    // -------------------------------------------------------------------------

    [Fact]
    public void ToString_ReturnsContentTypeString()
    {
        var contentType = ContentType.Create("image/png");

        contentType.ToString().ShouldBe("image/png");
    }

    // -------------------------------------------------------------------------
    // Top-level type / subtype decomposition + type predicates
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("image/png", "image", "png")]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application", "vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("text/plain", "text", "plain")]
    public void TopLevelTypeAndSubType_AreSplitOnSlash(string mime, string expectedTop, string expectedSub)
    {
        var contentType = ContentType.Create(mime);

        contentType.TopLevelType.ShouldBe(expectedTop);
        contentType.SubType.ShouldBe(expectedSub);
    }

    [Fact]
    public void TypePredicates_MatchTopLevelType()
    {
        ContentType.Create("image/png").IsImage.ShouldBeTrue();
        ContentType.Create("video/mp4").IsVideo.ShouldBeTrue();
        ContentType.Create("audio/mpeg").IsAudio.ShouldBeTrue();
        ContentType.Create("text/html").IsText.ShouldBeTrue();

        ContentType.Create("application/pdf").IsImage.ShouldBeFalse();
        ContentType.Create("image/png").IsVideo.ShouldBeFalse();
    }

    [Theory]
    [InlineData("IMAGE/PNG")]
    [InlineData("Image/Png")]
    public void TypePredicates_AreCaseInsensitive(string mime) => ContentType.Create(mime).IsImage.ShouldBeTrue();

    [Fact]
    public void IsTopLevelType_MatchesArbitraryType_CaseInsensitive()
    {
        var contentType = ContentType.Create("application/json");

        contentType.IsTopLevelType("application").ShouldBeTrue();
        contentType.IsTopLevelType("APPLICATION").ShouldBeTrue();
        contentType.IsTopLevelType("image").ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Inheritance
    // -------------------------------------------------------------------------

    [Fact]
    public void ContentType_InheritsSingleValueObject()
    {
        var contentType = ContentType.Create("text/html");

        contentType.ShouldBeAssignableTo<SingleValueObject<string>>();
        contentType.ShouldBeAssignableTo<ValueObject>();
    }
}
