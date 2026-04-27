using System.Diagnostics.Metrics;
using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Events;
using Granit.CustomerBalance.Internal;
using Granit.Events;
using Granit.Guids;
using Granit.Parties.Domain.ValueObjects;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.Tests.Internal;

public sealed class DefaultCreditExpirationServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IBalanceTransactionReader _transactionReader = Substitute.For<IBalanceTransactionReader>();
    private readonly IBalanceAccountReader _accountReader = Substitute.For<IBalanceAccountReader>();
    private readonly IBalanceAccountWriter _accountWriter = Substitute.For<IBalanceAccountWriter>();
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly Meter _meter = new("test");
    private readonly CustomerBalanceMetrics _metrics;
    private readonly DefaultCreditExpirationService _sut;

    public DefaultCreditExpirationServiceTests()
    {
        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(_meter);
        _metrics = new CustomerBalanceMetrics(meterFactory);

        _clock.Now.Returns(Now);
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());

        _sut = new DefaultCreditExpirationService(
            _transactionReader,
            _accountReader,
            _accountWriter,
            _eventBus,
            _guidGenerator,
            _clock,
            _metrics,
            NullLogger<DefaultCreditExpirationService>.Instance);
    }

    public void Dispose() => _meter.Dispose();

    // ======== No expired credits ========

    [Fact]
    public async Task ExpireCreditsAsync_NoExpiredCredits_ShouldReturnZero()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        _transactionReader.GetExpiredCreditsAsync(Now, Arg.Any<CancellationToken>())
            .Returns([]);

        int result = await _sut.ExpireCreditsAsync(ct);

        result.ShouldBe(0);
        await _accountWriter.DidNotReceiveWithAnyArgs().UpdateAsync(default!, ct);
    }

    // ======== Account not found ========

    [Fact]
    public async Task ExpireCreditsAsync_AccountNotFound_ShouldSkip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BalanceTransaction credit = CreateExpiredCredit();

        _transactionReader.GetExpiredCreditsAsync(Now, Arg.Any<CancellationToken>())
            .Returns([credit]);
        _accountReader.GetByIdAsync(credit.BalanceAccountId, Arg.Any<CancellationToken>())
            .Returns((BalanceAccount?)null);

        int result = await _sut.ExpireCreditsAsync(ct);

        result.ShouldBe(0);
    }

    // ======== Account with zero balance ========

    [Fact]
    public async Task ExpireCreditsAsync_ZeroBalance_ShouldSkip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BalanceTransaction credit = CreateExpiredCredit();
        var account = BalanceAccount.Create(credit.BalanceAccountId, Guid.NewGuid(), PartyId.Create(Guid.NewGuid()), "EUR");

        _transactionReader.GetExpiredCreditsAsync(Now, Arg.Any<CancellationToken>())
            .Returns([credit]);
        _accountReader.GetByIdAsync(credit.BalanceAccountId, Arg.Any<CancellationToken>())
            .Returns(account);

        int result = await _sut.ExpireCreditsAsync(ct);

        result.ShouldBe(0);
    }

    // ======== Successful expiration ========

    [Fact]
    public async Task ExpireCreditsAsync_WithBalance_ShouldDebitAndPublishEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        BalanceTransaction credit = CreateExpiredCredit();
        BalanceAccount account = CreateAccountWithBalance(credit.BalanceAccountId, tenantId, 100m);

        _transactionReader.GetExpiredCreditsAsync(Now, Arg.Any<CancellationToken>())
            .Returns([credit]);
        _accountReader.GetByIdAsync(credit.BalanceAccountId, Arg.Any<CancellationToken>())
            .Returns(account);

        int result = await _sut.ExpireCreditsAsync(ct);

        result.ShouldBe(1);
        await _accountWriter.Received(1).UpdateAsync(account, Arg.Any<CancellationToken>());
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<CreditExpiredEto>(e => e.Amount == 50m && e.Currency == "EUR"),
            Arg.Any<CancellationToken>());
    }

    // ======== Partial expiration (balance less than credit) ========

    [Fact]
    public async Task ExpireCreditsAsync_BalanceLessThanCredit_ShouldDebitOnlyAvailable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        BalanceTransaction credit = CreateExpiredCredit(amount: 200m);
        BalanceAccount account = CreateAccountWithBalance(credit.BalanceAccountId, tenantId, 80m);

        _transactionReader.GetExpiredCreditsAsync(Now, Arg.Any<CancellationToken>())
            .Returns([credit]);
        _accountReader.GetByIdAsync(credit.BalanceAccountId, Arg.Any<CancellationToken>())
            .Returns(account);

        int result = await _sut.ExpireCreditsAsync(ct);

        result.ShouldBe(1);
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<CreditExpiredEto>(e => e.Amount == 80m),
            Arg.Any<CancellationToken>());
    }

    // ======== Already expired (idempotency) ========

    [Fact]
    public async Task ExpireCreditsAsync_AlreadyExpired_ShouldSkip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        BalanceTransaction credit = CreateExpiredCredit();
        BalanceAccount account = CreateAccountWithBalance(credit.BalanceAccountId, tenantId, 100m);

        // Simulate an existing expiration transaction referencing this credit
        account.Debit(
            50m, TransactionSource.Expiration, "Already expired", Now.AddDays(-1),
            Guid.NewGuid(), referenceId: credit.Id, referenceType: "PromotionalCredit");

        _transactionReader.GetExpiredCreditsAsync(Now, Arg.Any<CancellationToken>())
            .Returns([credit]);
        _accountReader.GetByIdAsync(credit.BalanceAccountId, Arg.Any<CancellationToken>())
            .Returns(account);

        int result = await _sut.ExpireCreditsAsync(ct);

        result.ShouldBe(0);
    }

    // ======== Helpers ========

    private static BalanceTransaction CreateExpiredCredit(decimal amount = 50m)
    {
        return BalanceTransaction.Create(
            Guid.NewGuid(), Guid.NewGuid(), TransactionType.Credit, amount,
            TransactionSource.Promotional, "Promo", Now.AddDays(-30),
            expiresAt: Now.AddDays(-1));
    }

    private static BalanceAccount CreateAccountWithBalance(Guid accountId, Guid tenantId, decimal balance)
    {
        var account = BalanceAccount.Create(accountId, tenantId, PartyId.Create(Guid.NewGuid()), "EUR");
        account.Credit(balance, TransactionSource.Promotional, "Setup", Now.AddDays(-15), Guid.NewGuid());
        account.ClearIntegrationEvents();
        return account;
    }
}
