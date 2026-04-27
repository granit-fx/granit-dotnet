using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Events;
using Granit.CustomerBalance.Exceptions;
using Granit.Parties.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.Tests;

public sealed class BalanceAccountTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_ShouldInitializeWithZeroBalance()
    {
        BalanceAccount account = CreateAccount();

        account.Balance.ShouldBe(0m);
        account.Currency.ShouldBe("EUR");
        account.TenantId.ShouldNotBeNull();
        account.Transactions.Count.ShouldBe(0);
    }

    [Fact]
    public void Create_ShouldNormalizeCurrencyToUpperCase()
    {
        BalanceAccount account = BalanceAccount.Create(Guid.NewGuid(), Guid.NewGuid(), PartyId.Create(Guid.NewGuid()), "eur");

        account.Currency.ShouldBe("EUR");
    }

    [Fact]
    public void Create_WithEmptyCurrency_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            BalanceAccount.Create(Guid.NewGuid(), Guid.NewGuid(), PartyId.Create(Guid.NewGuid()), ""));
    }

    [Fact]
    public void Credit_ShouldIncreaseBalance()
    {
        BalanceAccount account = CreateAccount();

        account.Credit(50m, TransactionSource.Promotional, "Welcome credit", Now, Guid.NewGuid());

        account.Balance.ShouldBe(50m);
    }

    [Fact]
    public void Credit_ShouldAppendTransaction()
    {
        BalanceAccount account = CreateAccount();

        account.Credit(25m, TransactionSource.Overpayment, "Surplus", Now, Guid.NewGuid(),
            referenceId: Guid.NewGuid(), referenceType: "Invoice");

        account.Transactions.Count.ShouldBe(1);
        account.Transactions[0].Type.ShouldBe(TransactionType.Credit);
        account.Transactions[0].Amount.ShouldBe(25m);
        account.Transactions[0].Source.ShouldBe(TransactionSource.Overpayment);
        account.Transactions[0].ReferenceType.ShouldBe("Invoice");
    }

    [Fact]
    public void Credit_ShouldRaiseBalanceCreditedEto()
    {
        BalanceAccount account = CreateAccount();

        account.Credit(100m, TransactionSource.Promotional, "Promo", Now, Guid.NewGuid());

        account.IntegrationEvents.Count.ShouldBe(1);
        account.IntegrationEvents.First().ShouldBeOfType<BalanceCreditedEto>();
    }

    [Fact]
    public void Credit_WithExpiration_ShouldSetExpiresAt()
    {
        BalanceAccount account = CreateAccount();
        DateTimeOffset expiresAt = Now.AddDays(30);

        account.Credit(100m, TransactionSource.Promotional, "Promo", Now, Guid.NewGuid(),
            expiresAt: expiresAt);

        account.Transactions[0].ExpiresAt.ShouldBe(expiresAt);
    }

    [Fact]
    public void Credit_WithZeroAmount_ShouldThrow()
    {
        BalanceAccount account = CreateAccount();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            account.Credit(0m, TransactionSource.Promotional, "Zero", Now, Guid.NewGuid()));
    }

    [Fact]
    public void Debit_ShouldDecreaseBalance()
    {
        BalanceAccount account = CreateAccountWithBalance(100m);

        account.Debit(30m, TransactionSource.InvoiceDeduction, "Invoice deduction", Now, Guid.NewGuid());

        account.Balance.ShouldBe(70m);
    }

    [Fact]
    public void Debit_ShouldAppendTransaction()
    {
        BalanceAccount account = CreateAccountWithBalance(100m);
        var invoiceId = Guid.NewGuid();

        account.Debit(40m, TransactionSource.InvoiceDeduction, "Deduction", Now, Guid.NewGuid(),
            referenceId: invoiceId, referenceType: "Invoice");

        // 1 credit from setup + 1 debit
        account.Transactions.Count.ShouldBe(2);
        account.Transactions[1].Type.ShouldBe(TransactionType.Debit);
        account.Transactions[1].Amount.ShouldBe(40m);
        account.Transactions[1].ReferenceId.ShouldBe(invoiceId);
    }

    [Fact]
    public void Debit_ShouldRaiseBalanceDebitedEto()
    {
        BalanceAccount account = CreateAccountWithBalance(100m);

        account.Debit(50m, TransactionSource.InvoiceDeduction, "Deduction", Now, Guid.NewGuid());

        account.IntegrationEvents.ShouldContain(e => e is BalanceDebitedEto);
    }

    [Fact]
    public void Debit_ExceedingBalance_ShouldThrowInsufficientBalanceException()
    {
        BalanceAccount account = CreateAccountWithBalance(30m);

        InsufficientBalanceException ex = Should.Throw<InsufficientBalanceException>(() =>
            account.Debit(50m, TransactionSource.InvoiceDeduction, "Too much", Now, Guid.NewGuid()));

        ex.AvailableBalance.ShouldBe(30m);
        ex.RequestedAmount.ShouldBe(50m);
        ex.Currency.ShouldBe("EUR");
    }

    [Fact]
    public void Debit_ExactBalance_ShouldSucceed()
    {
        BalanceAccount account = CreateAccountWithBalance(100m);

        account.Debit(100m, TransactionSource.InvoiceDeduction, "Full deduction", Now, Guid.NewGuid());

        account.Balance.ShouldBe(0m);
    }

    [Fact]
    public void Debit_TransitionFromPositiveToZero_ShouldRaiseBalanceDepletedEto()
    {
        BalanceAccount account = CreateAccountWithBalance(100m);

        account.Debit(100m, TransactionSource.InvoiceDeduction, "Full deduction", Now, Guid.NewGuid());

        account.IntegrationEvents.ShouldContain(e => e is BalanceDepletedEto);
        BalanceDepletedEto eto = account.IntegrationEvents.OfType<BalanceDepletedEto>().Single();
        eto.BalanceAccountId.ShouldBe(account.Id);
        eto.PartyId.ShouldBe(account.PartyId.Value);
        eto.Currency.ShouldBe("EUR");
        eto.DepletedAt.ShouldBe(Now);
    }

    [Fact]
    public void Debit_PartialFromPositive_ShouldNotRaiseBalanceDepletedEto()
    {
        BalanceAccount account = CreateAccountWithBalance(100m);

        account.Debit(40m, TransactionSource.InvoiceDeduction, "Partial deduction", Now, Guid.NewGuid());

        account.IntegrationEvents.ShouldNotContain(e => e is BalanceDepletedEto);
    }

    [Fact]
    public void Debit_AlreadyAtZero_ShouldThrowAndNotRaiseBalanceDepletedEto()
    {
        // A no-op recompute that *attempts* to debit a zero balance is rejected by the
        // invariant — the transition guard never fires, so no BalanceDepletedEto is
        // emitted twice for the same balance.
        BalanceAccount account = CreateAccountWithBalance(100m);
        account.Debit(100m, TransactionSource.InvoiceDeduction, "Full deduction", Now, Guid.NewGuid());
        account.ClearIntegrationEvents();

        Should.Throw<Granit.CustomerBalance.Exceptions.InsufficientBalanceException>(() =>
            account.Debit(0.01m, TransactionSource.InvoiceDeduction, "No-op recompute", Now, Guid.NewGuid()));

        account.IntegrationEvents.ShouldNotContain(e => e is BalanceDepletedEto);
    }

    [Fact]
    public void MultipleOperations_ShouldMaintainCorrectBalance()
    {
        BalanceAccount account = CreateAccount();

        account.Credit(100m, TransactionSource.Promotional, "Promo", Now, Guid.NewGuid());
        account.Credit(50m, TransactionSource.Overpayment, "Surplus", Now, Guid.NewGuid());
        account.Debit(30m, TransactionSource.InvoiceDeduction, "Invoice 1", Now, Guid.NewGuid());
        account.Debit(20m, TransactionSource.InvoiceDeduction, "Invoice 2", Now, Guid.NewGuid());

        account.Balance.ShouldBe(100m);
        account.Transactions.Count.ShouldBe(4);
    }

    [Fact]
    public void Account_ShouldImplementIConcurrencyAware()
    {
        typeof(Granit.Domain.IConcurrencyAware)
            .IsAssignableFrom(typeof(BalanceAccount))
            .ShouldBeTrue();
    }

    [Fact]
    public void Account_ShouldImplementIMultiTenant()
    {
        typeof(BalanceAccount).GetInterfaces()
            .ShouldContain(i => i.Name == "IMultiTenant");
    }

    private static BalanceAccount CreateAccount() =>
        BalanceAccount.Create(Guid.NewGuid(), Guid.NewGuid(), PartyId.Create(Guid.NewGuid()), "EUR");

    private static BalanceAccount CreateAccountWithBalance(decimal balance)
    {
        BalanceAccount account = CreateAccount();
        account.Credit(balance, TransactionSource.Promotional, "Setup credit", Now, Guid.NewGuid());
        account.ClearIntegrationEvents();
        return account;
    }
}
