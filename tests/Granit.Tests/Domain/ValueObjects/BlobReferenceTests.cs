using Granit.Domain;
using Granit.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain.ValueObjects;

public sealed class BlobReferenceTests
{
    [Theory]
    [InlineData("personal-data-export/abc-123")]
    [InlineData("uploads/2026/05/photo.png")]
    [InlineData("a")]
    public void Create_ValidReference_Succeeds(string reference)
    {
        var result = BlobReference.Create(reference);

        result.Value.ShouldBe(reference);
    }

    [Fact]
    public void Create_NullOrWhitespace_Throws()
    {
        Should.Throw<ArgumentException>(() => BlobReference.Create(null!));
        Should.Throw<ArgumentException>(() => BlobReference.Create(""));
        Should.Throw<ArgumentException>(() => BlobReference.Create("   "));
    }

    [Fact]
    public void Create_ExceedsMaxLength_Throws()
    {
        string tooLong = new('a', BlobReference.MaxLength + 1);

        ArgumentException ex = Should.Throw<ArgumentException>(() => BlobReference.Create(tooLong));

        ex.Message.ShouldContain(BlobReference.MaxLength.ToString());
    }

    [Fact]
    public void Create_ExactlyMaxLength_Succeeds()
    {
        string exact = new('a', BlobReference.MaxLength);

        var result = BlobReference.Create(exact);

        result.Value.Length.ShouldBe(BlobReference.MaxLength);
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var reference = BlobReference.Create("uploads/file.bin");

        string result = reference;

        result.ShouldBe("uploads/file.bin");
    }

    [Fact]
    public void ImplicitConversion_FromString_CreatesInstance()
    {
        BlobReference reference = "uploads/file.bin";

        reference.Value.ShouldBe("uploads/file.bin");
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = BlobReference.Create("uploads/x");
        var b = BlobReference.Create("uploads/x");

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        var a = BlobReference.Create("uploads/a");
        var b = BlobReference.Create("uploads/b");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void ToString_ReturnsUnderlyingValue()
    {
        var reference = BlobReference.Create("uploads/x");

        reference.ToString().ShouldBe("uploads/x");
    }

    [Fact]
    public void BlobReference_InheritsSingleValueObject()
    {
        var reference = BlobReference.Create("uploads/x");

        reference.ShouldBeAssignableTo<SingleValueObject<string>>();
        reference.ShouldBeAssignableTo<ValueObject>();
    }
}
