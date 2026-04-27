using System.Diagnostics.Metrics;
using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Events;
using Granit.CustomerBalance.Internal;
using Granit.CustomerBalance.Options;
using Granit.Events;
using Granit.Parties.Domain.ValueObjects;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.Tests.Internal;

public sealed class DefaultCreditExpiringScanServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IBalanceTransactionReader _transactionReader = Substitute.For<IBalanceTransactionReader>();
    private readonly IBalanceTransactionWriter _transactionWriter = Substitute.For<IBalanceTransactionWriter>();
    private readonly IBalanceAccountReader _accountReader = Substitute.For<IBalanceAccountReader>();
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly Meter _meter = new("test");
    private readonly CustomerBalanceMetrics _metrics;
    private readonly CustomerBalanceOptions _options = new();
    private readonly DefaultCreditExpiringScanService _sut;

    public DefaultCreditExpiringScanServiceTests()
    {
        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(_meter);
        _metrics = new CustomerBalanceMetrics(meterFactory);

        _clock.Now.Returns(Now);

        _sut = new DefaultCreditExpiringScanService(
            _transactionReader,
            _transactionWriter,
            _accountReader,
            _eventBus,
            _clock,
            Microsoft.Extensions.Options.Options.Create(_options),
            _metrics,
            NullLogger<DefaultCreditExpiringScanService>.Instance);
    }

    public void Dispose() => _meter.Dispose();

    [Fact]
    public async Task ScanAsync_NoCandidates_ShouldReturnZero()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        _transactionReader.GetCreditsExpiringSoonAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);

        int result = await _sut.ScanAsync(ct);

        result.ShouldBe(0);
        await _eventBus.DidNotReceiveWithAnyArgs().PublishAsync<CreditExpiringEto>(default!, ct);
    }

    [Fact]
    public async Task ScanAsync_CandidateInWindow_ShouldEmitEtoAndStampNotifiedAt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        BalanceTransaction credit = CreateCredit(amount: 50m, expiresAt: Now.AddDays(3));
        BalanceAccount account = CreateAccountWithBalance(credit.BalanceAccountId, tenantId, 100m);

        _transactionReader.GetCreditsExpiringSoonAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([credit]);
        _accountReader.GetByIdAsync(credit.BalanceAccountId, Arg.Any<CancellationToken>())
            .Returns(account);

        int result = await _sut.ScanAsync(ct);

        result.ShouldBe(1);
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<CreditExpiringEto>(e =>
                e.BalanceAccountId == account.Id &&
                e.TenantId == tenantId &&
                e.PartyId == account.PartyId.Value &&
                e.CreditId == credit.Id &&
                e.Amount == 50m &&
                e.Currency == "EUR" &&
                e.ExpiresAt == credit.ExpiresAt!.Value),
            Arg.Any<CancellationToken>());
        await _transactionWriter.Received(1).StampExpirationNotifiedAsync(
            credit.Id, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScanAsync_AccountMissing_ShouldSkipCredit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BalanceTransaction credit = CreateCredit(amount: 50m, expiresAt: Now.AddDays(3));

        _transactionReader.GetCreditsExpiringSoonAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([credit]);
        _accountReader.GetByIdAsync(credit.BalanceAccountId, Arg.Any<CancellationToken>())
            .Returns((BalanceAccount?)null);

        int result = await _sut.ScanAsync(ct);

        result.ShouldBe(0);
        await _transactionWriter.DidNotReceiveWithAnyArgs()
            .StampExpirationNotifiedAsync(default, default, ct);
    }

    [Fact]
    public async Task ScanAsync_ShouldQueryReader_WithLeadTimeAndCooldownDerivedFromOptions()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _options.ExpirationLeadTimeDays = 14;
        _options.ExpirationNotificationCooldownDays = 5;

        _transactionReader.GetCreditsExpiringSoonAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _sut.ScanAsync(ct);

        await _transactionReader.Received(1).GetCreditsExpiringSoonAsync(
            Now,
            Now.AddDays(14),
            Now.AddDays(-5),
            Arg.Any<CancellationToken>());
    }

    private static BalanceTransaction CreateCredit(decimal amount, DateTimeOffset expiresAt) =>
        BalanceTransaction.Create(
            Guid.NewGuid(), Guid.NewGuid(), TransactionType.Credit, amount,
            TransactionSource.Promotional, "Promo", Now.AddDays(-30),
            expiresAt: expiresAt);

    private static BalanceAccount CreateAccountWithBalance(Guid accountId, Guid tenantId, decimal balance)
    {
        var account = BalanceAccount.Create(accountId, tenantId, PartyId.Create(Guid.NewGuid()), "EUR");
        account.Credit(balance, TransactionSource.Promotional, "Setup", Now.AddDays(-15), Guid.NewGuid());
        account.ClearIntegrationEvents();
        return account;
    }
}
