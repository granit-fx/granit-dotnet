using Granit.Core.Domain;
using Shouldly;
using Xunit;

namespace Granit.Core.Tests.Domain;

public sealed class SingleValueObjectTests
{
    // -------------------------------------------------------------------------
    // ToString
    // -------------------------------------------------------------------------

    [Fact]
    public void ToString_ReturnsUnderlyingValue()
    {
        TestStringWrapper wrapper = new() { Value = "hello" };

        wrapper.ToString().ShouldBe("hello");
    }

    [Fact]
    public void ToString_WithIntValue_ReturnsStringRepresentation()
    {
        TestIntWrapper wrapper = new() { Value = 42 };

        wrapper.ToString().ShouldBe("42");
    }

    // -------------------------------------------------------------------------
    // Equality — inherited from ValueObject via GetEqualityComponents
    // -------------------------------------------------------------------------

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        TestStringWrapper a = new() { Value = "abc" };
        TestStringWrapper b = new() { Value = "abc" };

        a.Equals(b).ShouldBeTrue();
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        TestStringWrapper a = new() { Value = "abc" };
        TestStringWrapper b = new() { Value = "xyz" };

        a.Equals(b).ShouldBeFalse();
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentSingleValueObjectTypes_ReturnsFalse()
    {
        TestStringWrapper stringWrapper = new() { Value = "42" };
        TestIntWrapper intWrapper = new() { Value = 42 };

        stringWrapper.Equals(intWrapper).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHash()
    {
        TestStringWrapper a = new() { Value = "test" };
        TestStringWrapper b = new() { Value = "test" };

        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValue_ReturnsDifferentHash()
    {
        TestStringWrapper a = new() { Value = "alpha" };
        TestStringWrapper b = new() { Value = "beta" };

        a.GetHashCode().ShouldNotBe(b.GetHashCode());
    }

    [Fact]
    public void CanBeUsedAsHashSetKey()
    {
        TestStringWrapper a = new() { Value = "same" };
        TestStringWrapper b = new() { Value = "same" };
        HashSet<TestStringWrapper> set = [a, b];

        set.Count.ShouldBe(1);
    }

    // -------------------------------------------------------------------------
    // GetEqualityComponents — sealed, yields only Value
    // -------------------------------------------------------------------------

    [Fact]
    public void Equality_UsesOnlyValueProperty()
    {
        // Two instances with same Value but potentially different object references
        TestIntWrapper a = new() { Value = 99 };
        TestIntWrapper b = new() { Value = 99 };

        a.ShouldBe(b);
    }

    // -------------------------------------------------------------------------
    // Null comparisons
    // -------------------------------------------------------------------------

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        TestStringWrapper wrapper = new() { Value = "hello" };

        wrapper.Equals(null).ShouldBeFalse();
        (wrapper == null).ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // InheritsValueObject
    // -------------------------------------------------------------------------

    [Fact]
    public void SingleValueObject_InheritsValueObject()
    {
        TestStringWrapper wrapper = new() { Value = "test" };

        wrapper.ShouldBeAssignableTo<ValueObject>();
    }

    // -------------------------------------------------------------------------
    // Test fixtures
    // -------------------------------------------------------------------------

    private sealed class TestStringWrapper : SingleValueObject<string>
    {
        public override required string Value { get; init; }
    }

    private sealed class TestIntWrapper : SingleValueObject<int>
    {
        public override required int Value { get; init; }
    }
}
