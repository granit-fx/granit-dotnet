using Granit.Domain;
using Granit.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain.ValueObjects;

public sealed class EntityTypeNameTests
{
    // -------------------------------------------------------------------------
    // Create — valid names
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Patient")]
    [InlineData("Granit.BlobStorage.Domain.BlobDescriptor")]
    [InlineData("MyEntity")]
    public void Create_ValidName_Succeeds(string name)
    {
        var result = EntityTypeName.Create(name);

        result.Value.ShouldBe(name);
    }

    // -------------------------------------------------------------------------
    // Create — null/whitespace
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_NullOrWhitespace_Throws()
    {
        Should.Throw<ArgumentException>(() => EntityTypeName.Create(null!));
        Should.Throw<ArgumentException>(() => EntityTypeName.Create(""));
        Should.Throw<ArgumentException>(() => EntityTypeName.Create("   "));
    }

    // -------------------------------------------------------------------------
    // Create — exceeds max length (500)
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_ExceedsMaxLength_Throws()
    {
        string longName = new('a', 501);

        ArgumentException ex = Should.Throw<ArgumentException>(() => EntityTypeName.Create(longName));

        ex.Message.ShouldContain("500");
    }

    [Fact]
    public void Create_ExactlyMaxLength_Succeeds()
    {
        string exactName = new('a', 500);

        var result = EntityTypeName.Create(exactName);

        result.Value.Length.ShouldBe(500);
    }

    // -------------------------------------------------------------------------
    // Implicit conversions
    // -------------------------------------------------------------------------

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var name = EntityTypeName.Create("Patient");

        string result = name;

        result.ShouldBe("Patient");
    }

    [Fact]
    public void ImplicitConversion_FromString_CreatesInstance()
    {
        EntityTypeName name = "Appointment";

        name.Value.ShouldBe("Appointment");
    }

    // -------------------------------------------------------------------------
    // Equality
    // -------------------------------------------------------------------------

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = EntityTypeName.Create("Patient");
        var b = EntityTypeName.Create("Patient");

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        var a = EntityTypeName.Create("Patient");
        var b = EntityTypeName.Create("Appointment");

        a.ShouldNotBe(b);
    }

    // -------------------------------------------------------------------------
    // ToString
    // -------------------------------------------------------------------------

    [Fact]
    public void ToString_ReturnsEntityTypeName()
    {
        var name = EntityTypeName.Create("Patient");

        name.ToString().ShouldBe("Patient");
    }

    // -------------------------------------------------------------------------
    // Inheritance
    // -------------------------------------------------------------------------

    [Fact]
    public void EntityTypeName_InheritsSingleValueObject()
    {
        var name = EntityTypeName.Create("Patient");

        name.ShouldBeAssignableTo<SingleValueObject<string>>();
        name.ShouldBeAssignableTo<ValueObject>();
    }
}
