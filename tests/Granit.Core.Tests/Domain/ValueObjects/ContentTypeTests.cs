using Granit.Core.Domain;
using Granit.Core.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Core.Tests.Domain.ValueObjects;

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
