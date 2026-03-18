using Granit.BackgroundJobs.Wolverine.Internal;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.BackgroundJobs.Wolverine.Tests;

public sealed class WolverineBackgroundJobDispatcherTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly WolverineBackgroundJobDispatcher _sut;

    public WolverineBackgroundJobDispatcherTests() => _sut = new(_bus);

    [Fact]
    public async Task PublishAsync_NoHeaders_PublishesWithNullDeliveryOptions()
    {
        object message = new { Name = "test" };

        await _sut.PublishAsync(message, cancellationToken: TestContext.Current.CancellationToken);

        // PublishAsync(T) is an extension that calls PublishAsync(T, null)
        await _bus.Received(1).PublishAsync(message, Arg.Is<DeliveryOptions?>(o => o == null));
    }

    [Fact]
    public async Task PublishAsync_EmptyHeaders_PublishesWithNullDeliveryOptions()
    {
        object message = new { Name = "test" };

        await _sut.PublishAsync(message, new Dictionary<string, string>(), TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(message, Arg.Is<DeliveryOptions?>(o => o == null));
    }

    [Fact]
    public async Task PublishAsync_WithHeaders_PublishesWithDeliveryOptions()
    {
        object message = new { Name = "test" };
        Dictionary<string, string> headers = new() { ["X-CorrelationId"] = "abc123", ["X-Source"] = "unit-test" };

        await _sut.PublishAsync(message, headers, TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(message, Arg.Is<DeliveryOptions>(o =>
            o.Headers["X-CorrelationId"] == "abc123" &&
            o.Headers["X-Source"] == "unit-test"));
    }

    [Fact]
    public async Task ScheduleAsync_PublishesWithScheduledDeliveryOptions()
    {
        object message = new { Name = "job" };
        DateTimeOffset scheduledTime = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

        await _sut.ScheduleAsync(message, scheduledTime, TestContext.Current.CancellationToken);

        // ScheduleAsync is an extension method that calls PublishAsync with DeliveryOptions.ScheduledTime
        await _bus.Received(1).PublishAsync(message, Arg.Is<DeliveryOptions>(o =>
            o.ScheduledTime == scheduledTime));
    }
}
