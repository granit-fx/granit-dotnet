using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain;

public sealed class ValueObjectTests
{
    [Fact]
    public void Equals_SameComponents_ReturnsTrue()
    {
        Money a = new() { Amount = 100m, Currency = "EUR" };
        Money b = new() { Amount = 100m, Currency = "EUR" };

        a.Equals(b).ShouldBeTrue();
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentComponents_ReturnsFalse()
    {
        Money a = new() { Amount = 100m, Currency = "EUR" };
        Money b = new() { Amount = 200m, Currency = "EUR" };

        a.Equals(b).ShouldBeFalse();
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentTypes_ReturnsFalse()
    {
        Money money = new() { Amount = 100m, Currency = "EUR" };
        Temperature temp = new() { Value = 100m, Unit = "EUR" };

        money.Equals(temp).ShouldBeFalse();
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        Money money = new() { Amount = 100m, Currency = "EUR" };

        money.Equals(null).ShouldBeFalse();
        (money == null).ShouldBeFalse();
        // Reverse operand order exercises the null-on-left path of operator ==(Money, Money).
        (null == money).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_SameComponents_ReturnsSameHash()
    {
        Money a = new() { Amount = 100m, Currency = "EUR" };
        Money b = new() { Amount = 100m, Currency = "EUR" };

        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentComponents_ReturnsDifferentHash()
    {
        Money a = new() { Amount = 100m, Currency = "EUR" };
        Money b = new() { Amount = 200m, Currency = "USD" };

        a.GetHashCode().ShouldNotBe(b.GetHashCode());
    }

    [Fact]
    public void Equals_WithNullComponent_HandlesCorrectly()
    {
        Address a = new() { Street = "Main St", City = null };
        Address b = new() { Street = "Main St", City = null };
        Address c = new() { Street = "Main St", City = "Paris" };

        a.Equals(b).ShouldBeTrue();
        a.Equals(c).ShouldBeFalse();
    }

    [Fact]
    public void BothNull_AreEqual() =>
        (null as Money == null as Money).ShouldBeTrue();

    [Fact]
    public void CanBeUsedAsHashSetKey()
    {
        Money a = new() { Amount = 100m, Currency = "EUR" };
        Money b = new() { Amount = 100m, Currency = "EUR" };
        HashSet<Money> set = [a, b];

        set.Count.ShouldBe(1);
    }

    // -------------------------------------------------------------------------
    // Test fixtures
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

    private sealed class Temperature : ValueObject
    {
        public decimal Value { get; init; }
        public string Unit { get; init; } = string.Empty;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Value;
            yield return Unit;
        }
    }

    private sealed class Address : ValueObject
    {
        public string Street { get; init; } = string.Empty;
        public string? City { get; init; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Street;
            yield return City;
        }
    }
}
