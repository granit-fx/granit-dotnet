using Granit.Core.Domain;
using Shouldly;
using Xunit;

namespace Granit.Core.Tests.Domain;

public sealed class ValueObjectEqualityComparerTests
{
    // -------------------------------------------------------------------------
    // IEqualityComparer<ValueObject>.Equals
    // -------------------------------------------------------------------------

    [Fact]
    public void IEqualityComparer_Equals_SameComponents_ReturnsTrue()
    {
        Money a = new() { Amount = 100m, Currency = "EUR" };
        Money b = new() { Amount = 100m, Currency = "EUR" };
        IEqualityComparer<ValueObject> comparer = a;

        comparer.Equals(a, b).ShouldBeTrue();
    }

    [Fact]
    public void IEqualityComparer_Equals_DifferentComponents_ReturnsFalse()
    {
        Money a = new() { Amount = 100m, Currency = "EUR" };
        Money b = new() { Amount = 200m, Currency = "USD" };
        IEqualityComparer<ValueObject> comparer = a;

        comparer.Equals(a, b).ShouldBeFalse();
    }

    [Fact]
    public void IEqualityComparer_Equals_BothNull_ReturnsTrue()
    {
        Money instance = new() { Amount = 0m, Currency = "" };
        IEqualityComparer<ValueObject> comparer = instance;

        comparer.Equals(null, null).ShouldBeTrue();
    }

    [Fact]
    public void IEqualityComparer_Equals_OneNull_ReturnsFalse()
    {
        Money a = new() { Amount = 100m, Currency = "EUR" };
        IEqualityComparer<ValueObject> comparer = a;

        comparer.Equals(a, null).ShouldBeFalse();
        comparer.Equals(null, a).ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // IEqualityComparer<ValueObject>.GetHashCode
    // -------------------------------------------------------------------------

    [Fact]
    public void IEqualityComparer_GetHashCode_SameAsInstanceHashCode()
    {
        Money a = new() { Amount = 100m, Currency = "EUR" };
        IEqualityComparer<ValueObject> comparer = a;

        comparer.GetHashCode(a).ShouldBe(a.GetHashCode());
    }

    // -------------------------------------------------------------------------
    // Equals(object) — sealed override
    // -------------------------------------------------------------------------

    [Fact]
    public void Equals_Object_SameValueObject_ReturnsTrue()
    {
        Money a = new() { Amount = 100m, Currency = "EUR" };
        Money b = new() { Amount = 100m, Currency = "EUR" };

        a.Equals((object)b).ShouldBeTrue();
    }

    [Fact]
    public void Equals_Object_NonValueObject_ReturnsFalse()
    {
        Money a = new() { Amount = 100m, Currency = "EUR" };

        a.Equals((object)"not a value object").ShouldBeFalse();
    }

    [Fact]
    public void Equals_Object_Null_ReturnsFalse()
    {
        Money a = new() { Amount = 100m, Currency = "EUR" };

        a.Equals((object?)null).ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Test fixture
    // -------------------------------------------------------------------------

    private sealed class Money : ValueObject
    {
        public decimal Amount { get; init; }
        public string Currency { get; init; } = string.Empty;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }
}
