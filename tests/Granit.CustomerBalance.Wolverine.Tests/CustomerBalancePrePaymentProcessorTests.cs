using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Wolverine.Internal;
using Granit.Guids;
using Granit.Invoicing;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Events;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.Wolverine.Tests;

public sealed class CustomerBalancePrePaymentProcessorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly IBalanceAccountReader _accountReader = Substitute.For<IBalanceAccountReader>();
    private readonly IBalanceAccountWriter _accountWriter = Substitute.For<IBalanceAccountWriter>();
    private readonly IInvoiceReader _invoiceReader = Substitute.For<IInvoiceReader>();
    private readonly IInvoiceWriter _invoiceWriter = Substitute.For<IInvoiceWriter>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly CustomerBalanceMetrics _metrics;
    private readonly CustomerBalancePrePaymentProcessor _processor;

    public CustomerBalancePrePaymentProcessorTests()
    {
        _clock.Now.Returns(Now);
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());

        System.Diagnostics.Metrics.IMeterFactory meterFactory = Substitute.For<System.Diagnostics.Metrics.IMeterFactory>();
        meterFactory.Create(Arg.Any<System.Diagnostics.Metrics.MeterOptions>())
            .Returns(new System.Diagnostics.Metrics.Meter("test"));
        _metrics = new CustomerBalanceMetrics(meterFactory);

        _processor = new CustomerBalancePrePaymentProcessor(
            _accountReader, _accountWriter, _invoiceReader, _invoiceWriter,
            _guidGenerator, _clock, _metrics,
            NullLogger<CustomerBalancePrePaymentProcessor>.Instance);
    }

    [Fact]
    public async Task ProcessAsync_NoAccount_ShouldReturnFullAmount()
    {
        InvoiceFinalizedEto eto = CreateEto(100m);
        _accountReader.GetByTenantAndCurrencyAsync(TenantId, "EUR", Arg.Any<CancellationToken>())
            .Returns((BalanceAccount?)null);

        PrePaymentResult result = await _processor.ProcessAsync(eto, TestContext.Current.CancellationToken);

        result.RemainingAmount.ShouldBe(100m);
        await _accountWriter.DidNotReceive().UpdateAsync(Arg.Any<BalanceAccount>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_ZeroBalance_ShouldReturnFullAmount()
    {
        InvoiceFinalizedEto eto = CreateEto(100m);
        BalanceAccount account = CreateAccount(0m);
        _accountReader.GetByTenantAndCurrencyAsync(TenantId, "EUR", Arg.Any<CancellationToken>())
            .Returns(account);

        PrePaymentResult result = await _processor.ProcessAsync(eto, TestContext.Current.CancellationToken);

        result.RemainingAmount.ShouldBe(100m);
    }

    [Fact]
    public async Task ProcessAsync_PartialBalance_ShouldDeductAndReturnRemainder()
    {
        InvoiceFinalizedEto eto = CreateEto(100m);
        BalanceAccount account = CreateAccount(30m);
        _accountReader.GetByTenantAndCurrencyAsync(TenantId, "EUR", Arg.Any<CancellationToken>())
            .Returns(account);
        _invoiceReader.GetByIdAsync(eto.InvoiceId, Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        PrePaymentResult result = await _processor.ProcessAsync(eto, TestContext.Current.CancellationToken);

        result.RemainingAmount.ShouldBe(70m);
        account.Balance.ShouldBe(0m);
        await _accountWriter.Received(1).UpdateAsync(account, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_FullCoverage_ShouldReturnZero()
    {
        InvoiceFinalizedEto eto = CreateEto(50m);
        BalanceAccount account = CreateAccount(200m);
        _accountReader.GetByTenantAndCurrencyAsync(TenantId, "EUR", Arg.Any<CancellationToken>())
            .Returns(account);
        _invoiceReader.GetByIdAsync(eto.InvoiceId, Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        PrePaymentResult result = await _processor.ProcessAsync(eto, TestContext.Current.CancellationToken);

        result.RemainingAmount.ShouldBe(0m);
        account.Balance.ShouldBe(150m);
    }

    [Fact]
    public async Task ProcessAsync_IdempotentRetry_ShouldSkipDebit()
    {
        var invoiceId = Guid.NewGuid();
        InvoiceFinalizedEto eto = CreateEto(100m, invoiceId);

        // Account already has a deduction for this invoice (previous attempt succeeded).
        BalanceAccount account = CreateAccount(70m);
        account.Debit(30m, TransactionSource.InvoiceDeduction, "Previous attempt", Now, Guid.NewGuid(),
            referenceId: invoiceId, referenceType: "Invoice");
        account.ClearIntegrationEvents();

        _accountReader.GetByTenantAndCurrencyAsync(TenantId, "EUR", Arg.Any<CancellationToken>())
            .Returns(account);
        _invoiceReader.GetByIdAsync(invoiceId, Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        PrePaymentResult result = await _processor.ProcessAsync(eto, TestContext.Current.CancellationToken);

        // Should use existing deduction amount (30), not debit again.
        result.RemainingAmount.ShouldBe(70m);
        await _accountWriter.DidNotReceive().UpdateAsync(Arg.Any<BalanceAccount>(), Arg.Any<CancellationToken>());
    }

    private static InvoiceFinalizedEto CreateEto(decimal total, Guid? invoiceId = null) =>
        new(invoiceId ?? Guid.NewGuid(), TenantId, total, "EUR", CollectionMethod.Auto);

    private static BalanceAccount CreateAccount(decimal balance)
    {
        var account = BalanceAccount.Create(Guid.NewGuid(), TenantId, "EUR");
        if (balance > 0)
        {
            account.Credit(balance, TransactionSource.Promotional, "Setup", Now, Guid.NewGuid());
            account.ClearIntegrationEvents();
        }

        return account;
    }
}
