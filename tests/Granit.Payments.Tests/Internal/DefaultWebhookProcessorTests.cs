using System.Text.Json;
using Granit.Payments.Commands;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Granit.Payments.Internal;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Payments.Tests.Internal;

public sealed class DefaultWebhookProcessorTests
{
    // ======== Fixtures ========

    private readonly IPaymentProvider _stripeProvider = Substitute.For<IPaymentProvider>();
    private readonly IPaymentTransactionReader _transactionReader = Substitute.For<IPaymentTransactionReader>();
    private readonly IPaymentTransactionWriter _transactionWriter = Substitute.For<IPaymentTransactionWriter>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ILogger<DefaultWebhookProcessor> _logger = NullLoggerFactory.Instance.CreateLogger<DefaultWebhookProcessor>();

    private readonly DefaultWebhookProcessor _sut;
    private static readonly DateTimeOffset Now = new(2026, 4, 5, 12, 0, 0, TimeSpan.Zero);

    public DefaultWebhookProcessorTests()
    {
        _stripeProvider.Name.Returns("stripe");
        _clock.Now.Returns(Now);

        _sut = new DefaultWebhookProcessor(
            [_stripeProvider],
            _transactionReader,
            _transactionWriter,
            _clock,
            _logger);
    }

    private static PaymentTransaction CreateTransaction(
        string providerName = "stripe",
        PaymentStatus advanceTo = PaymentStatus.Processing)
    {
        var tx = PaymentTransaction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            100m, "EUR", providerName, "card", $"idem_{Guid.NewGuid()}");

        if (advanceTo >= PaymentStatus.Processing)
        {
            tx.MarkProcessing();
        }

        return tx;
    }

    private static ProcessWebhookCommand CreateStripeCommand(string providerTransactionId)
    {
        JsonElement payload = JsonSerializer.Deserialize<JsonElement>($$"""
        {
            "data": {
                "object": {
                    "id": "{{providerTransactionId}}"
                }
            }
        }
        """);

        return new ProcessWebhookCommand("stripe", "payment_intent.succeeded", "evt_123", payload);
    }

    private static ProcessWebhookCommand CreateMollieCommand(string providerTransactionId)
    {
        JsonElement payload = JsonSerializer.Deserialize<JsonElement>($$"""
        {
            "id": "{{providerTransactionId}}"
        }
        """);

        return new ProcessWebhookCommand("mollie", "payment.paid", "evt_456", payload);
    }

    // ======== Happy Path: Succeeded ========

    [Fact]
    public async Task ProcessAsync_StripeSucceeded_ShouldUpdateTransaction()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PaymentTransaction tx = CreateTransaction();
        ProcessWebhookCommand command = CreateStripeCommand("pi_abc");

        _transactionReader.GetByProviderTransactionIdAsync("stripe", "pi_abc", ct).Returns(tx);
        _stripeProvider.GetStatusAsync("pi_abc", ct)
            .Returns(new PaymentProviderStatus("pi_abc", PaymentStatus.Succeeded));

        await _sut.ProcessAsync(command, ct);

        tx.Status.ShouldBe(PaymentStatus.Succeeded);
        tx.ProviderTransactionId.ShouldBe("pi_abc");
        await _transactionWriter.Received(1).UpdateAsync(tx, ct);
    }

    // ======== Happy Path: Failed ========

    [Fact]
    public async Task ProcessAsync_StripeFailed_ShouldUpdateTransaction()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PaymentTransaction tx = CreateTransaction();
        ProcessWebhookCommand command = CreateStripeCommand("pi_fail");

        _transactionReader.GetByProviderTransactionIdAsync("stripe", "pi_fail", ct).Returns(tx);
        _stripeProvider.GetStatusAsync("pi_fail", ct)
            .Returns(new PaymentProviderStatus("pi_fail", PaymentStatus.Failed));

        await _sut.ProcessAsync(command, ct);

        tx.Status.ShouldBe(PaymentStatus.Failed);
        await _transactionWriter.Received(1).UpdateAsync(tx, ct);
    }

    // ======== Happy Path: Canceled ========

    [Fact]
    public async Task ProcessAsync_StripeCanceled_ShouldUpdateTransaction()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PaymentTransaction tx = CreateTransaction(advanceTo: PaymentStatus.Created);
        ProcessWebhookCommand command = CreateStripeCommand("pi_cancel");

        _transactionReader.GetByProviderTransactionIdAsync("stripe", "pi_cancel", ct).Returns(tx);
        _stripeProvider.GetStatusAsync("pi_cancel", ct)
            .Returns(new PaymentProviderStatus("pi_cancel", PaymentStatus.Canceled));

        await _sut.ProcessAsync(command, ct);

        tx.Status.ShouldBe(PaymentStatus.Canceled);
        await _transactionWriter.Received(1).UpdateAsync(tx, ct);
    }

    // ======== No State Change ========

    [Fact]
    public async Task ProcessAsync_RequiresAction_ShouldNotUpdate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PaymentTransaction tx = CreateTransaction();
        ProcessWebhookCommand command = CreateStripeCommand("pi_action");

        _transactionReader.GetByProviderTransactionIdAsync("stripe", "pi_action", ct).Returns(tx);
        _stripeProvider.GetStatusAsync("pi_action", ct)
            .Returns(new PaymentProviderStatus("pi_action", PaymentStatus.RequiresAction));

        await _sut.ProcessAsync(command, ct);

        await _transactionWriter.DidNotReceive().UpdateAsync(Arg.Any<PaymentTransaction>(), Arg.Any<CancellationToken>());
    }

    // ======== Missing Transaction ID ========

    [Fact]
    public async Task ProcessAsync_EmptyPayload_ShouldReturnEarly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        JsonElement payload = JsonSerializer.Deserialize<JsonElement>("{}");
        var command = new ProcessWebhookCommand("stripe", "payment_intent.succeeded", "evt_no_id", payload);

        await _sut.ProcessAsync(command, ct);

        await _transactionReader.DidNotReceive()
            .GetByProviderTransactionIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ======== Transaction Not Found ========

    [Fact]
    public async Task ProcessAsync_TransactionNotFound_ShouldReturnEarly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ProcessWebhookCommand command = CreateStripeCommand("pi_orphan");
        _transactionReader.GetByProviderTransactionIdAsync("stripe", "pi_orphan", ct)
            .Returns((PaymentTransaction?)null);

        await _sut.ProcessAsync(command, ct);

        await _transactionWriter.DidNotReceive()
            .UpdateAsync(Arg.Any<PaymentTransaction>(), Arg.Any<CancellationToken>());
    }

    // ======== Provider Not Found ========

    [Fact]
    public async Task ProcessAsync_UnknownProvider_ShouldReturnEarly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PaymentTransaction tx = CreateTransaction();
        ProcessWebhookCommand command = CreateMollieCommand("tr_mol123");

        _transactionReader.GetByProviderTransactionIdAsync("mollie", "tr_mol123", ct).Returns(tx);
        // No mollie provider registered in _sut (only stripe)

        await _sut.ProcessAsync(command, ct);

        await _transactionWriter.DidNotReceive()
            .UpdateAsync(Arg.Any<PaymentTransaction>(), Arg.Any<CancellationToken>());
    }

    // ======== Mollie Payload Extraction ========

    [Fact]
    public async Task ProcessAsync_MolliePayload_ShouldExtractId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IPaymentProvider mollieProvider = Substitute.For<IPaymentProvider>();
        mollieProvider.Name.Returns("mollie");

        var sut = new DefaultWebhookProcessor(
            [_stripeProvider, mollieProvider],
            _transactionReader,
            _transactionWriter,
            _clock,
            _logger);

        PaymentTransaction tx = CreateTransaction(providerName: "mollie");
        ProcessWebhookCommand command = CreateMollieCommand("tr_mol456");

        _transactionReader.GetByProviderTransactionIdAsync("mollie", "tr_mol456", ct).Returns(tx);
        mollieProvider.GetStatusAsync("tr_mol456", ct)
            .Returns(new PaymentProviderStatus("tr_mol456", PaymentStatus.Succeeded));

        await sut.ProcessAsync(command, ct);

        tx.Status.ShouldBe(PaymentStatus.Succeeded);
        await _transactionWriter.Received(1).UpdateAsync(tx, ct);
    }

    // ======== Stripe Nested Payload ========

    [Fact]
    public async Task ProcessAsync_StripePayloadWithoutDataObject_ShouldFallbackToTopLevelId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        JsonElement payload = JsonSerializer.Deserialize<JsonElement>("""{ "id": "pi_fallback" }""");
        var command = new ProcessWebhookCommand("stripe", "payment_intent.succeeded", "evt_fb", payload);

        PaymentTransaction tx = CreateTransaction();
        _transactionReader.GetByProviderTransactionIdAsync("stripe", "pi_fallback", ct).Returns(tx);
        _stripeProvider.GetStatusAsync("pi_fallback", ct)
            .Returns(new PaymentProviderStatus("pi_fallback", PaymentStatus.Succeeded));

        await _sut.ProcessAsync(command, ct);

        tx.Status.ShouldBe(PaymentStatus.Succeeded);
    }
}
