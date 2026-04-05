using Granit.Payments.Domain;
using Granit.Payments.Domain.ValueObjects;
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
            PaymentMethods.Card, "stripe", "pm_123",
            "**** 4242", DateTimeOffset.UtcNow.AddYears(2));

        method.Type.ShouldBe(PaymentMethods.Card);
        method.DisplayLabel.ShouldBe("**** 4242");
        method.IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void SetDefault_ShouldSetFlag()
    {
        var method = PaymentMethod.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            PaymentMethods.Card, "stripe", "pm_123", "**** 4242");

        method.SetDefault();

        method.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void UnsetDefault_ShouldClearFlag()
    {
        var method = PaymentMethod.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            PaymentMethods.Card, "stripe", "pm_123", "**** 4242");

        method.SetDefault();
        method.UnsetDefault();

        method.IsDefault.ShouldBeFalse();
    }

    // ======== TransactionId value object ========

    [Fact]
    public void TransactionId_Create_ShouldReturnInstance()
    {
        var guid = Guid.NewGuid();

        var id = TransactionId.Create(guid);

        id.Value.ShouldBe(guid);
    }

    [Fact]
    public void TransactionId_Create_WithEmptyGuid_ShouldThrow() =>
        Should.Throw<ArgumentException>(() => TransactionId.Create(Guid.Empty));

    [Fact]
    public void TransactionId_ImplicitConversion_ShouldRoundTrip()
    {
        var guid = Guid.NewGuid();
        TransactionId id = guid;
        Guid result = id;

        result.ShouldBe(guid);
    }

    // ======== PaymentMethodId value object ========

    [Fact]
    public void PaymentMethodId_Create_ShouldReturnInstance()
    {
        var guid = Guid.NewGuid();

        var id = PaymentMethodId.Create(guid);

        id.Value.ShouldBe(guid);
    }

    [Fact]
    public void PaymentMethodId_Create_WithEmptyGuid_ShouldThrow() =>
        Should.Throw<ArgumentException>(() => PaymentMethodId.Create(Guid.Empty));

    [Fact]
    public void PaymentMethodId_ImplicitConversion_ShouldRoundTrip()
    {
        var guid = Guid.NewGuid();
        PaymentMethodId id = guid;
        Guid result = id;

        result.ShouldBe(guid);
    }
}
