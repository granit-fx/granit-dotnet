using System.Diagnostics.Metrics;
using Granit.Contacts.Domain.ValueObjects;
using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Exceptions;
using Granit.CustomerBalance.Internal;
using Granit.Guids;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.Tests.Internal;

public sealed class DefaultAdminDebitServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IBalanceAccountReader _accountReader = Substitute.For<IBalanceAccountReader>();
    private readonly IBalanceAccountWriter _accountWriter = Substitute.For<IBalanceAccountWriter>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly Meter _meter = new("test");
    private readonly CustomerBalanceMetrics _metrics;
    private readonly DefaultAdminDebitService _sut;

    public DefaultAdminDebitServiceTests()
    {
        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(_meter);
        _metrics = new CustomerBalanceMetrics(meterFactory);

        _clock.Now.Returns(Now);
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());

        _sut = new DefaultAdminDebitService(
            _accountReader,
            _accountWriter,
            _guidGenerator,
            _clock,
            _metrics,
            NullLogger<DefaultAdminDebitService>.Instance);
    }

    public void Dispose() => _meter.Dispose();

    private BalanceAccount AccountWithBalance(Guid tenantId, ContactId contactId, string currency, decimal balance)
    {
        var account = BalanceAccount.Create(Guid.NewGuid(), tenantId, contactId, currency);
        if (balance > 0m)
        {
            account.Credit(
                balance, TransactionSource.ManualAdjustment, "seed",
                Now.AddDays(-1), Guid.NewGuid());
        }

        _accountReader.GetByContactAndCurrencyAsync(contactId, currency, Arg.Any<CancellationToken>())
            .Returns(account);
        return account;
    }

    [Fact]
    public async Task DebitAsync_HappyPath_ReducesBalanceAndPersists()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var contactId = ContactId.Create(Guid.NewGuid());
        BalanceAccount account = AccountWithBalance(tenantId, contactId, "EUR", balance: 100m);

        BalanceAccount result = await _sut.DebitAsync(
            tenantId, contactId, 30m, "EUR", "Manual correction", referenceId: null, referenceType: null, ct);

        result.Balance.ShouldBe(70m);
        result.Transactions.Count.ShouldBe(2);   // seed credit + this debit
        result.Transactions[^1].Type.ShouldBe(TransactionType.Debit);
        result.Transactions[^1].Source.ShouldBe(TransactionSource.ManualAdjustment);
        result.Transactions[^1].Reason.ShouldBe("Manual correction");
        await _accountWriter.Received(1).UpdateAsync(account, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DebitAsync_NoAccount_Throws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var contactId = ContactId.Create(Guid.NewGuid());
        _accountReader.GetByContactAndCurrencyAsync(contactId, "EUR", Arg.Any<CancellationToken>())
            .Returns((BalanceAccount?)null);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            _sut.DebitAsync(tenantId, contactId, 10m, "EUR", "x", null, null, ct));

        await _accountWriter.DidNotReceiveWithAnyArgs().UpdateAsync(default!, ct);
    }

    [Fact]
    public async Task DebitAsync_InsufficientBalance_ThrowsAndDoesNotWrite()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var contactId = ContactId.Create(Guid.NewGuid());
        AccountWithBalance(tenantId, contactId, "EUR", balance: 20m);

        await Should.ThrowAsync<InsufficientBalanceException>(() =>
            _sut.DebitAsync(tenantId, contactId, 50m, "EUR", "x", null, null, ct));

        await _accountWriter.DidNotReceiveWithAnyArgs().UpdateAsync(default!, ct);
    }

    [Fact]
    public async Task DebitAsync_SameReferenceId_IsIdempotent_NoDoubleDebit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var contactId = ContactId.Create(Guid.NewGuid());
        var refId = Guid.NewGuid();
        BalanceAccount account = AccountWithBalance(tenantId, contactId, "EUR", balance: 100m);

        // First call: real debit
        await _sut.DebitAsync(tenantId, contactId, 30m, "EUR", "first", referenceId: refId, "AdminAdjustment", ct);
        account.Balance.ShouldBe(70m);

        // Second call with the same refId: short-circuited, no second debit
        BalanceAccount second = await _sut.DebitAsync(
            tenantId, contactId, 30m, "EUR", "retry", referenceId: refId, "AdminAdjustment", ct);

        second.Balance.ShouldBe(70m);                          // unchanged
        account.Transactions.Count(t => t.Type == TransactionType.Debit).ShouldBe(1);
        await _accountWriter.Received(1).UpdateAsync(account, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DebitAsync_DifferentReferenceIds_AppliesEach()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var contactId = ContactId.Create(Guid.NewGuid());
        BalanceAccount account = AccountWithBalance(tenantId, contactId, "EUR", balance: 100m);

        await _sut.DebitAsync(tenantId, contactId, 20m, "EUR", "a", referenceId: Guid.NewGuid(), "x", ct);
        await _sut.DebitAsync(tenantId, contactId, 30m, "EUR", "b", referenceId: Guid.NewGuid(), "x", ct);

        account.Balance.ShouldBe(50m);
        account.Transactions.Count(t => t.Type == TransactionType.Debit).ShouldBe(2);
    }

    [Fact]
    public async Task DebitAsync_NullReferenceId_AlwaysAppliesEvenOnRepeat()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var contactId = ContactId.Create(Guid.NewGuid());
        BalanceAccount account = AccountWithBalance(tenantId, contactId, "EUR", balance: 100m);

        // No idempotency without referenceId — both debits land.
        await _sut.DebitAsync(tenantId, contactId, 10m, "EUR", "first", null, null, ct);
        await _sut.DebitAsync(tenantId, contactId, 10m, "EUR", "second", null, null, ct);

        account.Balance.ShouldBe(80m);
    }
}
