using System.Diagnostics.Metrics;
using System.Text.Json;
using Granit.Notifications.Sse.StackExchangeRedis.Diagnostics;
using Granit.Notifications.Sse.StackExchangeRedis.Internal;
using Granit.Notifications.Sse.StackExchangeRedis.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Granit.Notifications.Sse.StackExchangeRedis.Tests;

public sealed class RedisSseBackplaneTests
{
    private readonly IConnectionMultiplexer _multiplexer = Substitute.For<IConnectionMultiplexer>();
    private readonly ISubscriber _subscriber = Substitute.For<ISubscriber>();
    private readonly ISseConnectionManager _connectionManager = Substitute.For<ISseConnectionManager>();

    private RedisSseBackplane CreateSut()
    {
        _multiplexer.GetSubscriber(Arg.Any<object?>()).Returns(_subscriber);

        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(ci => new Meter(ci.Arg<MeterOptions>().Name));

        return new RedisSseBackplane(
            _multiplexer,
            _connectionManager,
            Microsoft.Extensions.Options.Options.Create(new SseRedisBackplaneOptions()),
            new SseRedisBackplaneMetrics(meterFactory),
            NullLogger<RedisSseBackplane>.Instance);
    }

    private static string ValidEnvelope(string userId = "user-1") =>
        JsonSerializer.Serialize(new RedisSseBackplane.SseEnvelope(
            userId,
            new SseNotificationMessage { NotificationTypeName = "test.notification" }));

    [Fact]
    public async Task DeliverLocally_ValidEnvelope_ReachesLocalConnections()
    {
        await CreateSut().DeliverLocallyAsync(ValidEnvelope());

        await _connectionManager.Received(1).SendToUserAsync(
            "user-1",
            Arg.Is<SseNotificationMessage>(m => m.NotificationTypeName == "test.notification"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("{not valid json")]
    [InlineData("42")]
    [InlineData("{\"UserId\":\"\",\"Message\":{}}")]
    public async Task DeliverLocally_PoisonMessage_IsDropped_AndTheLoopSurvives(string payload)
    {
        RedisSseBackplane sut = CreateSut();

        // A poison message must never throw out of the handler…
        await Should.NotThrowAsync(async () => await sut.DeliverLocallyAsync(payload));

        // …and the very next valid message still delivers (the listen loop survived).
        await sut.DeliverLocallyAsync(ValidEnvelope("user-2"));
        await _connectionManager.Received(1).SendToUserAsync(
            "user-2", Arg.Any<SseNotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeliverLocally_LocalDeliveryFailure_IsDropped_NotRethrown()
    {
#pragma warning disable CA2012 // NSubstitute When(...) requires invoking the ValueTask member unawaited
        _connectionManager
            .When(m => m.SendToUserAsync(Arg.Any<string>(), Arg.Any<SseNotificationMessage>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("connection torn down"));
#pragma warning restore CA2012

        await Should.NotThrowAsync(async () => await CreateSut().DeliverLocallyAsync(ValidEnvelope()));
    }

    [Fact]
    public async Task Publish_SerializesTheEnvelopeOnTheConfiguredChannel()
    {
        RedisValue captured = default;
        _subscriber.PublishAsync(Arg.Any<RedisChannel>(), Arg.Do<RedisValue>(v => captured = v), Arg.Any<CommandFlags>())
            .Returns(0L);

        await CreateSut().PublishAsync(
            "user-1",
            new SseNotificationMessage { NotificationTypeName = "test.notification" },
            TestContext.Current.CancellationToken);

        RedisSseBackplane.SseEnvelope? envelope =
            JsonSerializer.Deserialize<RedisSseBackplane.SseEnvelope>((string)captured!);
        envelope.ShouldNotBeNull();
        envelope.UserId.ShouldBe("user-1");
        envelope.Message.NotificationTypeName.ShouldBe("test.notification");
    }
}
