using System.Threading.Channels;
using Granit.BackgroundJobs.Internal;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests.Internal;

public sealed class ChannelBackgroundJobDispatcherTests
{
    private readonly Channel<BackgroundJobEnvelope> _channel = Channel.CreateUnbounded<BackgroundJobEnvelope>();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly ChannelBackgroundJobDispatcher _sut;

    public ChannelBackgroundJobDispatcherTests()
    {
        _timeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        _sut = new ChannelBackgroundJobDispatcher(_channel, _timeProvider);
    }

    [Fact]
    public async Task PublishAsync_WritesEnvelopeToChannel()
    {
        // Arrange
        object message = new { Name = "test" };
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act
        await _sut.PublishAsync(message, cancellationToken: ct);

        // Assert
        bool hasItem = _channel.Reader.TryRead(out BackgroundJobEnvelope? envelope);
        hasItem.ShouldBeTrue();
        envelope!.Message.ShouldBeSameAs(message);
        envelope.Headers.ShouldBeNull();
    }

    [Fact]
    public async Task PublishAsync_WithHeaders_WritesEnvelopeWithHeaders()
    {
        // Arrange
        object message = new { Name = "test" };
        Dictionary<string, string> headers = new() { ["X-Test"] = "value" };
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act
        await _sut.PublishAsync(message, headers, ct);

        // Assert
        bool hasItem = _channel.Reader.TryRead(out BackgroundJobEnvelope? envelope);
        hasItem.ShouldBeTrue();
        envelope!.Headers.ShouldNotBeNull();
        envelope.Headers!["X-Test"].ShouldBe("value");
    }

    [Fact]
    public async Task ScheduleAsync_PastTime_WritesImmediately()
    {
        // Arrange
        object message = new { Name = "test" };
        DateTimeOffset pastTime = _timeProvider.GetUtcNow().AddMinutes(-5);
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act
        await _sut.ScheduleAsync(message, pastTime, ct);

        // Assert
        bool hasItem = _channel.Reader.TryRead(out BackgroundJobEnvelope? envelope);
        hasItem.ShouldBeTrue();
        envelope!.Message.ShouldBeSameAs(message);
    }

    [Fact]
    public async Task ScheduleAsync_FutureTime_DelaysBeforeWriting()
    {
        // Arrange
        object message = new { Name = "test" };
        DateTimeOffset futureTime = _timeProvider.GetUtcNow().AddMinutes(5);
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act — start the scheduling task
        Task scheduleTask = _sut.ScheduleAsync(message, futureTime, ct);

        // Assert — nothing in channel yet (delayed)
        bool hasItemBefore = _channel.Reader.TryRead(out _);
        hasItemBefore.ShouldBeFalse();

        // Advance time past the scheduled time
        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        await scheduleTask;

        // Assert — now the message should be in the channel
        bool hasItemAfter = _channel.Reader.TryRead(out BackgroundJobEnvelope? envelope);
        hasItemAfter.ShouldBeTrue();
        envelope!.Message.ShouldBeSameAs(message);
    }
}
