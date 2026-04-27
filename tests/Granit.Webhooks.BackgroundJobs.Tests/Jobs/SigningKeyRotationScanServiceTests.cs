using Granit.Events;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.BackgroundJobs.Services;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Events;
using Granit.Webhooks.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.BackgroundJobs.Tests.Jobs;

public sealed class SigningKeyRotationScanServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 27, 7, 0, 0, TimeSpan.Zero);

    private readonly IWebhookSigningKeyReader _reader = Substitute.For<IWebhookSigningKeyReader>();
    private readonly IWebhookSigningKeyWriter _writer = Substitute.For<IWebhookSigningKeyWriter>();
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IOptions<WebhooksOptions> _options =
        Microsoft.Extensions.Options.Options.Create(new WebhooksOptions { RotationLeadTimeDays = 14 });

    public SigningKeyRotationScanServiceTests() =>
        _clock.Now.Returns(Now);

    private SigningKeyRotationScanService CreateSut() => new(
        _reader, _writer, _eventBus, _clock, _options,
        NullLogger<SigningKeyRotationScanService>.Instance);

    [Fact]
    public async Task ExecuteAsync_NoExpiringKeys_PublishesNothing()
    {
        _reader.GetExpiringSoonAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns([]);

        await CreateSut().ExecuteAsync(TestContext.Current.CancellationToken);

        await _eventBus.DidNotReceiveWithAnyArgs().PublishAsync<WebhookSigningKeyRotationDueEto>(default!, Arg.Any<CancellationToken>());
        await _writer.DidNotReceiveWithAnyArgs().StampRotationNotificationAsync(default, default, default, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_QueriesReader_WithLeadTimeAndDedupeWindow()
    {
        _reader.GetExpiringSoonAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns([]);

        await CreateSut().ExecuteAsync(TestContext.Current.CancellationToken);

        await _reader.Received(1).GetExpiringSoonAsync(
            Now,
            Now.AddDays(14),
            Now - TimeSpan.FromDays(7),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_KeyExpiringSoon_PublishesEtoAndStamps()
    {
        var subscriptionId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        DateTimeOffset expiresAt = Now.AddDays(10);

        var key = WebhookSigningKey.Create(
            keyId, subscriptionId, "protected_secret", Now.AddDays(-30),
            WebhookSigningKeyStatus.Active, expiresAt);

        _reader.GetExpiringSoonAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>())
            .Returns([key]);

        await CreateSut().ExecuteAsync(TestContext.Current.CancellationToken);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<WebhookSigningKeyRotationDueEto>(e =>
                e.SubscriptionId == subscriptionId
                && e.KeyId == keyId
                && e.ExpiresAt == expiresAt),
            Arg.Any<CancellationToken>());

        await _writer.Received(1).StampRotationNotificationAsync(
            subscriptionId, keyId, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PublishesBeforeStamping_ToPreserveAtLeastOnceSemantics()
    {
        // Verify call ordering: stamp must come AFTER publish so a publish failure does not
        // skip the next scanner run via a premature dedupe stamp.
        var key = WebhookSigningKey.Create(
            Guid.NewGuid(), Guid.NewGuid(), "secret", Now.AddDays(-1),
            WebhookSigningKeyStatus.Active, Now.AddDays(5));

        _reader.GetExpiringSoonAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs([key]);

        var calls = new List<string>();
        _eventBus.PublishAsync(Arg.Any<WebhookSigningKeyRotationDueEto>(), Arg.Any<CancellationToken>())
            .Returns(_ => { calls.Add("publish"); return Task.CompletedTask; });
        _writer.StampRotationNotificationAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(_ => { calls.Add("stamp"); return Task.CompletedTask; });

        await CreateSut().ExecuteAsync(TestContext.Current.CancellationToken);

        calls.ShouldBe(["publish", "stamp"]);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleKeys_PublishesOnePerKey()
    {
        WebhookSigningKey[] keys =
        [
            WebhookSigningKey.Create(Guid.NewGuid(), Guid.NewGuid(), "s1", Now.AddDays(-30),
                WebhookSigningKeyStatus.Active, Now.AddDays(3)),
            WebhookSigningKey.Create(Guid.NewGuid(), Guid.NewGuid(), "s2", Now.AddDays(-30),
                WebhookSigningKeyStatus.Retired, Now.AddDays(7)),
        ];

        _reader.GetExpiringSoonAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(keys);

        await CreateSut().ExecuteAsync(TestContext.Current.CancellationToken);

        await _eventBus.Received(2).PublishAsync(
            Arg.Any<WebhookSigningKeyRotationDueEto>(), Arg.Any<CancellationToken>());
        await _writer.Received(2).StampRotationNotificationAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Now, Arg.Any<CancellationToken>());
    }
}
