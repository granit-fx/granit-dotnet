using Granit.Payments.Domain;
using Shouldly;
using Xunit;

namespace Granit.Payments.Tests;

public sealed class PaymentMethodTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var method = PaymentMethod.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            PaymentMethodType.Card, "stripe", "pm_123",
            "**** 4242", DateTimeOffset.UtcNow.AddYears(2));

        method.Type.ShouldBe(PaymentMethodType.Card);
        method.DisplayLabel.ShouldBe("**** 4242");
        method.IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void SetDefault_ShouldSetFlag()
    {
        var method = PaymentMethod.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            PaymentMethodType.Card, "stripe", "pm_123", "**** 4242");

        method.SetDefault();

        method.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void UnsetDefault_ShouldClearFlag()
    {
        var method = PaymentMethod.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            PaymentMethodType.Card, "stripe", "pm_123", "**** 4242");

        method.SetDefault();
        method.UnsetDefault();

        method.IsDefault.ShouldBeFalse();
    }
}
