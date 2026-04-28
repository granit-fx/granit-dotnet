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

public sealed class DefaultAdminCreditServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IBalanceAccountReader _accountReader = Substitute.For<IBalanceAccountReader>();
    private readonly IBalanceAccountWriter _accountWriter = Substitute.For<IBalanceAccountWriter>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly Meter _meter = new("test");
    private readonly CustomerBalanceMetrics _metrics;
    private readonly DefaultAdminCreditService _sut;

    public DefaultAdminCreditServiceTests()
    {
        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(_meter);
        _metrics = new CustomerBalanceMetrics(meterFactory);

        _clock.Now.Returns(Now);
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());

        _sut = new DefaultAdminCreditService(
            _accountReader,
            _accountWriter,
            _guidGenerator,
            _clock,
            _metrics,
            NullLogger<DefaultAdminCreditService>.Instance);
    }

    public void Dispose() => _meter.Dispose();

    // ======== Existing account ========

    [Fact]
    public async Task ApplyAsync_ExistingAccount_ShouldCreditAndUpdate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var partyId = PartyId.Create(Guid.NewGuid());
        var account = BalanceAccount.Create(Guid.NewGuid(), tenantId, partyId, "EUR");

        _accountReader.GetByPartyAndCurrencyAsync(partyId, "EUR", Arg.Any<CancellationToken>())
            .Returns(account);

        BalanceAccount result = await _sut.ApplyAsync(
            tenantId, partyId, 100m, "EUR", TransactionSource.ManualAdjustment, "Test credit", null, ct);

        result.ShouldBe(account);
        result.Balance.ShouldBe(100m);
        result.Transactions.Count.ShouldBe(1);
        result.Transactions[0].Source.ShouldBe(TransactionSource.ManualAdjustment);
        result.Transactions[0].Reason.ShouldBe("Test credit");
        await _accountWriter.Received(1).UpdateAsync(account, Arg.Any<CancellationToken>());
        await _accountWriter.DidNotReceiveWithAnyArgs().AddAsync(default!, ct);
    }

    // ======== New account creation ========

    [Fact]
    public async Task ApplyAsync_NoAccount_ShouldCreateThenCredit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();

        var partyId = PartyId.Create(Guid.NewGuid());

        var newAccount = BalanceAccount.Create(Guid.NewGuid(), tenantId, partyId, "EUR");
        _accountReader.GetByPartyAndCurrencyAsync(partyId, "EUR", Arg.Any<CancellationToken>())
            .Returns(null as BalanceAccount, newAccount);

        BalanceAccount result = await _sut.ApplyAsync(
            tenantId, partyId, 50m, "EUR", TransactionSource.Promotional, "Welcome bonus", null, ct);

        await _accountWriter.Received(1).AddAsync(Arg.Any<BalanceAccount>(), Arg.Any<CancellationToken>());
        await _accountWriter.Received(1).UpdateAsync(newAccount, Arg.Any<CancellationToken>());
        result.Balance.ShouldBe(50m);
    }

    // ======== Promotional source ========

    [Fact]
    public async Task ApplyAsync_PromotionalSource_ShouldSetCorrectTransactionSource()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var partyId = PartyId.Create(Guid.NewGuid());
        var account = BalanceAccount.Create(Guid.NewGuid(), tenantId, partyId, "USD");

        _accountReader.GetByPartyAndCurrencyAsync(partyId, "USD", Arg.Any<CancellationToken>())
            .Returns(account);

        await _sut.ApplyAsync(
            tenantId, partyId, 200m, "USD", TransactionSource.Promotional, "Promo credit", null, ct);

        account.Transactions[0].Source.ShouldBe(TransactionSource.Promotional);
    }

    // ======== Expiration date ========

    [Fact]
    public async Task ApplyAsync_WithExpirationDate_ShouldPassToTransaction()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var partyId = PartyId.Create(Guid.NewGuid());
        var account = BalanceAccount.Create(Guid.NewGuid(), tenantId, partyId, "EUR");
        DateTimeOffset expiresAt = Now.AddDays(30);

        _accountReader.GetByPartyAndCurrencyAsync(partyId, "EUR", Arg.Any<CancellationToken>())
            .Returns(account);

        await _sut.ApplyAsync(
            tenantId, partyId, 75m, "EUR", TransactionSource.Promotional, "Limited promo", expiresAt, ct);

        account.Transactions[0].ExpiresAt.ShouldBe(expiresAt);
    }

    // ======== No expiration ========

    [Fact]
    public async Task ApplyAsync_WithoutExpirationDate_ShouldHaveNullExpiration()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var partyId = PartyId.Create(Guid.NewGuid());
        var account = BalanceAccount.Create(Guid.NewGuid(), tenantId, partyId, "EUR");

        _accountReader.GetByPartyAndCurrencyAsync(partyId, "EUR", Arg.Any<CancellationToken>())
            .Returns(account);

        await _sut.ApplyAsync(
            tenantId, partyId, 50m, "EUR", TransactionSource.ManualAdjustment, "Permanent credit", null, ct);

        account.Transactions[0].ExpiresAt.ShouldBeNull();
    }

    // ======== Multiple credits accumulate ========

    [Fact]
    public async Task ApplyAsync_MultipleCalls_ShouldAccumulateBalance()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var partyId = PartyId.Create(Guid.NewGuid());
        var account = BalanceAccount.Create(Guid.NewGuid(), tenantId, partyId, "EUR");

        _accountReader.GetByPartyAndCurrencyAsync(partyId, "EUR", Arg.Any<CancellationToken>())
            .Returns(account);

        await _sut.ApplyAsync(
            tenantId, partyId, 100m, "EUR", TransactionSource.ManualAdjustment, "First", null, ct);
        await _sut.ApplyAsync(
            tenantId, partyId, 50m, "EUR", TransactionSource.Promotional, "Second", null, ct);

        account.Balance.ShouldBe(150m);
        account.Transactions.Count.ShouldBe(2);
    }

    // ======== Returns updated account ========

    [Fact]
    public async Task ApplyAsync_ShouldReturnAccountWithUpdatedBalance()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var partyId = PartyId.Create(Guid.NewGuid());
        var account = BalanceAccount.Create(Guid.NewGuid(), tenantId, partyId, "EUR");

        _accountReader.GetByPartyAndCurrencyAsync(partyId, "EUR", Arg.Any<CancellationToken>())
            .Returns(account);

        BalanceAccount result = await _sut.ApplyAsync(
            tenantId, partyId, 42.50m, "EUR", TransactionSource.RefundCredit, "Refund", null, ct);

        result.Balance.ShouldBe(42.50m);
        result.Currency.ShouldBe("EUR");
    }

    // ======== Reason propagation ========

    [Fact]
    public async Task ApplyAsync_ShouldPropagateReasonToTransaction()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var partyId = PartyId.Create(Guid.NewGuid());
        var account = BalanceAccount.Create(Guid.NewGuid(), tenantId, partyId, "EUR");

        _accountReader.GetByPartyAndCurrencyAsync(partyId, "EUR", Arg.Any<CancellationToken>())
            .Returns(account);

        await _sut.ApplyAsync(
            tenantId, partyId, 10m, "EUR", TransactionSource.ManualAdjustment, "Custom reason text", null, ct);

        account.Transactions[0].Reason.ShouldBe("Custom reason text");
    }
}
