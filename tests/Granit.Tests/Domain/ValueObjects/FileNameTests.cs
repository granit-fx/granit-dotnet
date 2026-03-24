using Granit.Domain;
using Granit.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain.ValueObjects;

public sealed class FileNameTests
{
    // -------------------------------------------------------------------------
    // Create — valid file names
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("report.pdf")]
    [InlineData("image.png")]
    [InlineData("my-document_v2.docx")]
    [InlineData("file")]
    public void Create_ValidFileName_Succeeds(string name)
    {
        var result = FileName.Create(name);

        result.Value.ShouldBe(name);
    }

    // -------------------------------------------------------------------------
    // Create — null/whitespace
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_NullOrWhitespace_Throws()
    {
        Should.Throw<ArgumentException>(() => FileName.Create(null!));
        Should.Throw<ArgumentException>(() => FileName.Create(""));
        Should.Throw<ArgumentException>(() => FileName.Create("   "));
    }

    // -------------------------------------------------------------------------
    // Create — exceeds max length (260)
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_ExceedsMaxLength_Throws()
    {
        string longName = new('a', 261);

        ArgumentException ex = Should.Throw<ArgumentException>(() => FileName.Create(longName));

        ex.Message.ShouldContain("260");
    }

    [Fact]
    public void Create_ExactlyMaxLength_Succeeds()
    {
        string exactName = new('a', 260);

        var result = FileName.Create(exactName);

        result.Value.Length.ShouldBe(260);
    }

    // -------------------------------------------------------------------------
    // Create — path traversal sequences
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("..\\windows\\system32")]
    [InlineData("path/to/file.txt")]
    [InlineData("path\\to\\file.txt")]
    [InlineData("file..name")]
    public void Create_PathTraversal_Throws(string name)
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() => FileName.Create(name));

        ex.Message.ShouldContain("path traversal");
    }

    // -------------------------------------------------------------------------
    // Implicit conversions
    // -------------------------------------------------------------------------

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var fileName = FileName.Create("report.pdf");

        string result = fileName;

        result.ShouldBe("report.pdf");
    }

    [Fact]
    public void ImplicitConversion_FromString_CreatesInstance()
    {
        FileName fileName = "document.docx";

        fileName.Value.ShouldBe("document.docx");
    }

    // -------------------------------------------------------------------------
    // Equality
    // -------------------------------------------------------------------------

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = FileName.Create("file.txt");
        var b = FileName.Create("file.txt");

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        var a = FileName.Create("file1.txt");
        var b = FileName.Create("file2.txt");

        a.ShouldNotBe(b);
    }

    // -------------------------------------------------------------------------
    // ToString
    // -------------------------------------------------------------------------

    [Fact]
    public void ToString_ReturnsFileNameString()
    {
        var fileName = FileName.Create("report.pdf");

        fileName.ToString().ShouldBe("report.pdf");
    }

    // -------------------------------------------------------------------------
    // Inheritance
    // -------------------------------------------------------------------------

    [Fact]
    public void FileName_InheritsSingleValueObject()
    {
        var fileName = FileName.Create("test.txt");

        fileName.ShouldBeAssignableTo<SingleValueObject<string>>();
        fileName.ShouldBeAssignableTo<ValueObject>();
    }
}
