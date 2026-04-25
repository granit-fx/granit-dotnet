using System.Diagnostics.Metrics;
using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Events;
using Granit.CustomerBalance.Internal;
using Granit.Events;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.Tests.Internal;

public sealed class DefaultPreExpirationScanServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 24, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset TodayStart = new(2026, 4, 24, 0, 0, 0, TimeSpan.Zero);

    private readonly IBalanceTransactionReader _transactionReader = Substitute.For<IBalanceTransactionReader>();
    private readonly IBalanceAccountReader _accountReader = Substitute.For<IBalanceAccountReader>();
    private readonly IBalanceAccountWriter _accountWriter = Substitute.For<IBalanceAccountWriter>();
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly Meter _meter = new("test");
    private readonly CustomerBalanceMetrics _metrics;
    private readonly DefaultPreExpirationScanService _sut;

    public DefaultPreExpirationScanServiceTests()
    {
        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(_meter);
        _metrics = new CustomerBalanceMetrics(meterFactory);

        _clock.Now.Returns(Now);

        IOptions<CustomerBalanceOptions> options =
            Options.Create(new CustomerBalanceOptions { PreExpirationWarningDays = 7 });

        _sut = new DefaultPreExpirationScanService(
            _transactionReader, _accountReader, _accountWriter,
            _eventBus, _clock, options, _metrics,
            NullLogger<DefaultPreExpirationScanService>.Instance);
    }

    public void Dispose() => _meter.Dispose();

    private static (BalanceAccount Account, BalanceTransaction Credit) NewAccountWithExpiringCredit(
        DateTimeOffset expiresAt, decimal amount = 50m)
    {
        var tenantId = Guid.NewGuid();
        var account = BalanceAccount.Create(Guid.NewGuid(), tenantId, "EUR");
        account.Credit(
            amount, TransactionSource.Promotional, "Welcome",
            createdAt: Now.AddDays(-30), transactionId: Guid.NewGuid(),
            expiresAt: expiresAt);
        return (account, account.Transactions[0]);
    }

    [Fact]
    public async Task ScanAsync_NoCredits_ReturnsZero()
    {
        _transactionReader
            .GetCreditsNearExpirationAsync(Now, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns([]);

        int published = await _sut.ScanAsync(TestContext.Current.CancellationToken);

        published.ShouldBe(0);
        await _eventBus.DidNotReceiveWithAnyArgs().PublishAsync<CreditNearExpirationEto>(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ScanAsync_PublishesEventAndMarksNoticed()
    {
        DateTimeOffset expiresIn5Days = Now.AddDays(5);
        (BalanceAccount account, BalanceTransaction credit) = NewAccountWithExpiringCredit(expiresIn5Days);

        _transactionReader
            .GetCreditsNearExpirationAsync(Now, TimeSpan.FromDays(7), Arg.Any<CancellationToken>())
            .Returns([credit]);
        _accountReader
            .GetByIdAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(account);

        int published = await _sut.ScanAsync(TestContext.Current.CancellationToken);

        published.ShouldBe(1);
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<CreditNearExpirationEto>(e =>
                e.BalanceAccountId == account.Id
                && e.TenantId == account.TenantId
                && e.CreditTransactionId == credit.Id
                && e.ExpiringAmount == credit.Amount
                && e.Currency == "EUR"
                && e.ExpiresAt == expiresIn5Days
                && e.DaysUntilExpiration == 5),
            Arg.Any<CancellationToken>());
        credit.LastPreExpirationNoticedAt.ShouldBe(Now);
        await _accountWriter.Received(1).UpdateAsync(account, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScanAsync_DaysUntilExpiration_FloorsTheFractionalValue()
    {
        // 5 days + 18 hours from Now → DaysUntilExpiration should be 5 (floored), not 6.
        DateTimeOffset expires = Now.AddDays(5).AddHours(18);
        (BalanceAccount account, BalanceTransaction credit) = NewAccountWithExpiringCredit(expires);

        _transactionReader
            .GetCreditsNearExpirationAsync(Now, TimeSpan.FromDays(7), Arg.Any<CancellationToken>())
            .Returns([credit]);
        _accountReader.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        await _sut.ScanAsync(TestContext.Current.CancellationToken);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<CreditNearExpirationEto>(e => e.DaysUntilExpiration == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScanAsync_AlreadyExpiredCredit_Skipped()
    {
        // The reader's filter excludes ExpiresAt <= now, but defensively the service
        // re-checks. Simulate a race where the reader returns a stale row.
        DateTimeOffset expired = Now.AddSeconds(-1);
        (BalanceAccount account, BalanceTransaction credit) = NewAccountWithExpiringCredit(expired);

        _transactionReader
            .GetCreditsNearExpirationAsync(Now, TimeSpan.FromDays(7), Arg.Any<CancellationToken>())
            .Returns([credit]);

        int published = await _sut.ScanAsync(TestContext.Current.CancellationToken);

        published.ShouldBe(0);
        await _eventBus.DidNotReceiveWithAnyArgs().PublishAsync<CreditNearExpirationEto>(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ScanAsync_AccountVanished_Skipped()
    {
        (BalanceAccount _, BalanceTransaction credit) = NewAccountWithExpiringCredit(Now.AddDays(3));

        _transactionReader
            .GetCreditsNearExpirationAsync(Now, TimeSpan.FromDays(7), Arg.Any<CancellationToken>())
            .Returns([credit]);
        _accountReader
            .GetByIdAsync(credit.BalanceAccountId, Arg.Any<CancellationToken>())
            .Returns((BalanceAccount?)null);

        int published = await _sut.ScanAsync(TestContext.Current.CancellationToken);

        published.ShouldBe(0);
    }

    [Fact]
    public async Task ScanAsync_HonoursConfiguredWarningDays()
    {
        // When PreExpirationWarningDays = 3, the reader should be queried with TimeSpan.FromDays(3).
        IOptions<CustomerBalanceOptions> options =
            Options.Create(new CustomerBalanceOptions { PreExpirationWarningDays = 3 });

        var sut = new DefaultPreExpirationScanService(
            _transactionReader, _accountReader, _accountWriter,
            _eventBus, _clock, options, _metrics,
            NullLogger<DefaultPreExpirationScanService>.Instance);

        _transactionReader
            .GetCreditsNearExpirationAsync(Now, TimeSpan.FromDays(3), Arg.Any<CancellationToken>())
            .Returns([]);

        await sut.ScanAsync(TestContext.Current.CancellationToken);

        await _transactionReader.Received(1).GetCreditsNearExpirationAsync(
            Now, TimeSpan.FromDays(3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScanAsync_NonPositiveWarningDays_ClampedToOne()
    {
        IOptions<CustomerBalanceOptions> options =
            Options.Create(new CustomerBalanceOptions { PreExpirationWarningDays = 0 });

        var sut = new DefaultPreExpirationScanService(
            _transactionReader, _accountReader, _accountWriter,
            _eventBus, _clock, options, _metrics,
            NullLogger<DefaultPreExpirationScanService>.Instance);

        _transactionReader
            .GetCreditsNearExpirationAsync(Now, TimeSpan.FromDays(1), Arg.Any<CancellationToken>())
            .Returns([]);

        await sut.ScanAsync(TestContext.Current.CancellationToken);

        await _transactionReader.Received(1).GetCreditsNearExpirationAsync(
            Now, TimeSpan.FromDays(1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BalanceTransaction_MarkPreExpirationNoticed_SetsTimestamp()
    {
        (BalanceAccount _, BalanceTransaction credit) = NewAccountWithExpiringCredit(Now.AddDays(3));
        credit.LastPreExpirationNoticedAt.ShouldBeNull();

        credit.MarkPreExpirationNoticed(Now);

        credit.LastPreExpirationNoticedAt.ShouldBe(Now);
    }

    // Sanity: ensure TodayStart constant is consistent with the convention used by
    // the EF reader's "today" floor (UTC midnight of `now.Date`).
    [Fact]
    public void TodayStart_IsUtcMidnightOfNowDate() =>
        TodayStart.ShouldBe(new DateTimeOffset(Now.Date, TimeSpan.Zero));
}
