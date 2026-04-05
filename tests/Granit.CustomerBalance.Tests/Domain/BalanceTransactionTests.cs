using Granit.CustomerBalance.Domain;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.Tests.Domain;

public sealed class BalanceTransactionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    // ======== Create — valid ========

    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var id = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        DateTimeOffset expiresAt = Now.AddDays(30);

        var tx = BalanceTransaction.Create(
            id, accountId, TransactionType.Credit, 50m, TransactionSource.Promotional,
            "Welcome credit", Now, referenceId, "Campaign", expiresAt);

        tx.Id.ShouldBe(id);
        tx.BalanceAccountId.ShouldBe(accountId);
        tx.Type.ShouldBe(TransactionType.Credit);
        tx.Amount.ShouldBe(50m);
        tx.Source.ShouldBe(TransactionSource.Promotional);
        tx.Reason.ShouldBe("Welcome credit");
        tx.CreatedAt.ShouldBe(Now);
        tx.ReferenceId.ShouldBe(referenceId);
        tx.ReferenceType.ShouldBe("Campaign");
        tx.ExpiresAt.ShouldBe(expiresAt);
    }

    [Fact]
    public void Create_WithoutOptionalFields_ShouldSetNulls()
    {
        var tx = BalanceTransaction.Create(
            Guid.NewGuid(), Guid.NewGuid(), TransactionType.Debit, 10m,
            TransactionSource.InvoiceDeduction, "Deduction", Now);

        tx.ReferenceId.ShouldBeNull();
        tx.ReferenceType.ShouldBeNull();
        tx.ExpiresAt.ShouldBeNull();
    }

    // ======== Create — validation ========

    [Fact]
    public void Create_WithZeroAmount_ShouldThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            BalanceTransaction.Create(
                Guid.NewGuid(), Guid.NewGuid(), TransactionType.Credit, 0m,
                TransactionSource.Promotional, "Zero", Now));
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            BalanceTransaction.Create(
                Guid.NewGuid(), Guid.NewGuid(), TransactionType.Credit, -10m,
                TransactionSource.Promotional, "Negative", Now));
    }

    [Fact]
    public void Create_WithEmptyReason_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            BalanceTransaction.Create(
                Guid.NewGuid(), Guid.NewGuid(), TransactionType.Credit, 50m,
                TransactionSource.Promotional, "", Now));
    }

    [Fact]
    public void Create_WithNullReason_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            BalanceTransaction.Create(
                Guid.NewGuid(), Guid.NewGuid(), TransactionType.Credit, 50m,
                TransactionSource.Promotional, null!, Now));
    }
}
