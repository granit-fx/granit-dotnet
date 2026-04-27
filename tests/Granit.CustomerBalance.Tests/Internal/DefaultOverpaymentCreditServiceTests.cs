using System.Diagnostics.Metrics;
using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Internal;
using Granit.Guids;
using Granit.Parties.Domain.ValueObjects;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.Tests.Internal;

public sealed class DefaultOverpaymentCreditServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IBalanceAccountReader _accountReader = Substitute.For<IBalanceAccountReader>();
    private readonly IBalanceAccountWriter _accountWriter = Substitute.For<IBalanceAccountWriter>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly Meter _meter = new("test");
    private readonly CustomerBalanceMetrics _metrics;
    private readonly DefaultOverpaymentCreditService _sut;

    public DefaultOverpaymentCreditServiceTests()
    {
        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(_meter);
        _metrics = new CustomerBalanceMetrics(meterFactory);

        _clock.Now.Returns(Now);
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());

        _sut = new DefaultOverpaymentCreditService(
            _accountReader,
            _accountWriter,
            _guidGenerator,
            _clock,
            _metrics,
            NullLogger<DefaultOverpaymentCreditService>.Instance);
    }

    public void Dispose() => _meter.Dispose();

    // ======== Existing account ========

    [Fact]
    public async Task CreditOverpaymentAsync_ExistingAccount_ShouldCreditAndUpdate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var contactId = PartyId.Create(Guid.NewGuid());
        var invoiceId = Guid.NewGuid();
        var account = BalanceAccount.Create(Guid.NewGuid(), tenantId, contactId, "EUR");

        _accountReader.GetByContactAndCurrencyAsync(contactId, "EUR", Arg.Any<CancellationToken>())
            .Returns(account);

        await _sut.CreditOverpaymentAsync(
            tenantId, contactId, "EUR", 42.50m, invoiceId, ct);

        account.Balance.ShouldBe(42.50m);
        account.Transactions.Count.ShouldBe(1);
        account.Transactions[0].Source.ShouldBe(TransactionSource.Overpayment);
        account.Transactions[0].ReferenceId.ShouldBe(invoiceId);
        account.Transactions[0].ReferenceType.ShouldBe("Invoice");
        await _accountWriter.Received(1).UpdateAsync(account, Arg.Any<CancellationToken>());
        await _accountWriter.DidNotReceiveWithAnyArgs().AddAsync(default!, ct);
    }

    // ======== New account creation ========

    [Fact]
    public async Task CreditOverpaymentAsync_NoAccount_ShouldCreateThenCredit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var contactId = PartyId.Create(Guid.NewGuid());
        var invoiceId = Guid.NewGuid();

        // First call returns null (account does not exist), second call returns the newly created account.
        var newAccount = BalanceAccount.Create(Guid.NewGuid(), tenantId, contactId, "EUR");
        _accountReader.GetByContactAndCurrencyAsync(contactId, "EUR", Arg.Any<CancellationToken>())
            .Returns(null as BalanceAccount, newAccount);

        await _sut.CreditOverpaymentAsync(
            tenantId, contactId, "EUR", 75m, invoiceId, ct);

        await _accountWriter.Received(1).AddAsync(Arg.Any<BalanceAccount>(), Arg.Any<CancellationToken>());
        await _accountWriter.Received(1).UpdateAsync(newAccount, Arg.Any<CancellationToken>());
        newAccount.Balance.ShouldBe(75m);
    }

    // ======== Amount propagation ========

    [Fact]
    public async Task CreditOverpaymentAsync_ShouldPassCorrectAmountToAccount()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var contactId = PartyId.Create(Guid.NewGuid());
        var account = BalanceAccount.Create(Guid.NewGuid(), tenantId, contactId, "USD");

        _accountReader.GetByContactAndCurrencyAsync(contactId, "USD", Arg.Any<CancellationToken>())
            .Returns(account);

        await _sut.CreditOverpaymentAsync(
            tenantId, contactId, "USD", 123.45m, Guid.NewGuid(), ct);

        account.Balance.ShouldBe(123.45m);
        account.Transactions[0].Reason.ShouldBe("Overpayment on invoice");
    }

    // ======== Multiple overpayments ========

    [Fact]
    public async Task CreditOverpaymentAsync_MultipleCalls_ShouldAccumulate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var contactId = PartyId.Create(Guid.NewGuid());
        var account = BalanceAccount.Create(Guid.NewGuid(), tenantId, contactId, "EUR");

        _accountReader.GetByContactAndCurrencyAsync(contactId, "EUR", Arg.Any<CancellationToken>())
            .Returns(account);

        await _sut.CreditOverpaymentAsync(
            tenantId, contactId, "EUR", 10m, Guid.NewGuid(), ct);
        await _sut.CreditOverpaymentAsync(
            tenantId, contactId, "EUR", 25m, Guid.NewGuid(), ct);

        account.Balance.ShouldBe(35m);
        account.Transactions.Count.ShouldBe(2);
    }
}
