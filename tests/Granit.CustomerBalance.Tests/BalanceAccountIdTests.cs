using Granit.CustomerBalance.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.Tests;

public sealed class BalanceAccountIdTests
{
    [Fact]
    public void Create_WithValidGuid_ShouldSucceed()
    {
        var value = Guid.NewGuid();

        var id = BalanceAccountId.Create(value);

        id.Value.ShouldBe(value);
    }

    [Fact]
    public void Create_WithEmptyGuid_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => BalanceAccountId.Create(Guid.Empty));
    }

    [Fact]
    public void ImplicitConversion_ToGuid_ShouldWork()
    {
        var value = Guid.NewGuid();
        var id = BalanceAccountId.Create(value);

        Guid result = id;

        result.ShouldBe(value);
    }

    [Fact]
    public void ImplicitConversion_FromGuid_ShouldWork()
    {
        var value = Guid.NewGuid();

        BalanceAccountId id = value;

        id.Value.ShouldBe(value);
    }
}
