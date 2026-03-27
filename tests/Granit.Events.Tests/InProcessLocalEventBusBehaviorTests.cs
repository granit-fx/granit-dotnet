// =============================================================================
// Tests - InProcessLocalEventBus (Behavioral)
// =============================================================================
// Covers behavioral scenarios not addressed by the base and additional tests:
//   - Cancellation token is forwarded to handlers
//   - Handler execution order matches DI registration order
//   - Concurrent publishing is safe
//   - Handler exception does not prevent metrics recording
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Events.Diagnostics;
using Granit.Events.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Events.Tests;

public sealed class InProcessLocalEventBusBehaviorTests : IDisposable
{
    // =========================================================================
    // Test doubles
    // =========================================================================

    private sealed record TestEvent(string Value);

    private sealed class TokenCapturingHandler : ILocalEventHandler<TestEvent>
    {
        public CancellationToken CapturedToken { get; private set; }

        public Task HandleAsync(TestEvent localEvent, CancellationToken cancellationToken = default)
        {
            CapturedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class OrderTrackingHandler(List<int> executionOrder, int id) : ILocalEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent localEvent, CancellationToken cancellationToken = default)
        {
            executionOrder.Add(id);
            return Task.CompletedTask;
        }
    }

    private sealed class CountingHandler : ILocalEventHandler<TestEvent>
    {
        private int _callCount;

        public int CallCount => _callCount;

        public Task HandleAsync(TestEvent localEvent, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            return Task.CompletedTask;
        }
    }

    // =========================================================================
    // Infrastructure
    // =========================================================================

    private readonly ServiceProvider _metricsSp;
    private readonly IMeterFactory _meterFactory;
    private readonly EventsMetrics _metrics;

    public InProcessLocalEventBusBehaviorTests()
    {
        ServiceCollection metricsServices = new();
        metricsServices.AddMetrics();
        _metricsSp = metricsServices.BuildServiceProvider();
        _meterFactory = _metricsSp.GetRequiredService<IMeterFactory>();
        _metrics = new EventsMetrics(_meterFactory);
    }

    public void Dispose() => _metricsSp.Dispose();

    // =========================================================================
    // Cancellation token propagation
    // =========================================================================

    [Fact]
    public async Task PublishAsync_CancellationToken_ForwardedToHandler()
    {
        // Arrange
        TokenCapturingHandler handler = new();
        ServiceCollection services = new();
        services.AddSingleton<ILocalEventHandler<TestEvent>>(handler);
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessLocalEventBus bus = new(sp, NullLogger<InProcessLocalEventBus>.Instance, _metrics);

        using CancellationTokenSource cts = new();
        CancellationToken token = cts.Token;

        // Act
        await bus.PublishAsync(new TestEvent("token-test"), token);

        // Assert
        handler.CapturedToken.ShouldBe(token);
    }

    [Fact]
    public async Task PublishAsync_DefaultCancellationToken_ForwardedAsNone()
    {
        // Arrange
        TokenCapturingHandler handler = new();
        ServiceCollection services = new();
        services.AddSingleton<ILocalEventHandler<TestEvent>>(handler);
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessLocalEventBus bus = new(sp, NullLogger<InProcessLocalEventBus>.Instance, _metrics);

        // Act — intentionally omitting CancellationToken to verify default propagation
#pragma warning disable xUnit1051
        await bus.PublishAsync(new TestEvent("default-token"));
#pragma warning restore xUnit1051

        // Assert
        handler.CapturedToken.ShouldBe(CancellationToken.None);
    }

    // =========================================================================
    // Handler execution order
    // =========================================================================

    [Fact]
    public async Task PublishAsync_MultipleHandlers_ExecutedInRegistrationOrder()
    {
        // Arrange
        List<int> executionOrder = [];
        ServiceCollection services = new();
        services.AddSingleton<ILocalEventHandler<TestEvent>>(new OrderTrackingHandler(executionOrder, 1));
        services.AddSingleton<ILocalEventHandler<TestEvent>>(new OrderTrackingHandler(executionOrder, 2));
        services.AddSingleton<ILocalEventHandler<TestEvent>>(new OrderTrackingHandler(executionOrder, 3));
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessLocalEventBus bus = new(sp, NullLogger<InProcessLocalEventBus>.Instance, _metrics);

        // Act
        await bus.PublishAsync(new TestEvent("order-test"), TestContext.Current.CancellationToken);

        // Assert
        executionOrder.ShouldBe([1, 2, 3]);
    }

    // =========================================================================
    // Concurrent publishing
    // =========================================================================

    [Fact]
    public async Task PublishAsync_ConcurrentCalls_AllHandlersInvoked()
    {
        // Arrange
        CountingHandler handler = new();
        ServiceCollection services = new();
        services.AddSingleton<ILocalEventHandler<TestEvent>>(handler);
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessLocalEventBus bus = new(sp, NullLogger<InProcessLocalEventBus>.Instance, _metrics);

        const int concurrentPublishes = 50;

        // Act — fire multiple publishes concurrently
        Task[] tasks = Enumerable.Range(0, concurrentPublishes)
            .Select(i => bus.PublishAsync(new TestEvent($"concurrent-{i}"), TestContext.Current.CancellationToken))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert
        handler.CallCount.ShouldBe(concurrentPublishes);
    }

    // =========================================================================
    // Metrics recording on handler failure
    // =========================================================================

    [Fact]
    public async Task PublishAsync_HandlerThrows_RecordsPublishedAndErrorMetrics()
    {
        // Arrange
        using MetricCollector<long> publishedCollector = new(
            _meterFactory, EventsMetrics.MeterName, "granit.events.event.published");
        using MetricCollector<long> handlerCollector = new(
            _meterFactory, EventsMetrics.MeterName, "granit.events.handler.executed");

        ServiceCollection services = new();
        services.AddSingleton<ILocalEventHandler<TestEvent>>(new ThrowingHandler());
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessLocalEventBus bus = new(sp, NullLogger<InProcessLocalEventBus>.Instance, _metrics);

        // Act
        await bus.PublishAsync(new TestEvent("metrics-test"), TestContext.Current.CancellationToken);

        // Assert — published metric recorded regardless of handler failure
        IReadOnlyList<CollectedMeasurement<long>> published = publishedCollector.GetMeasurementSnapshot();
        published.ShouldHaveSingleItem();
        published[0].Tags["bus_type"].ShouldBe("local");
        published[0].Tags["event_type"].ShouldBe("TestEvent");

        // Assert — handler error metric recorded
        IReadOnlyList<CollectedMeasurement<long>> handlers = handlerCollector.GetMeasurementSnapshot();
        handlers.ShouldHaveSingleItem();
        handlers[0].Tags["status"].ShouldBe("error");
    }

    // =========================================================================
    // Shared test doubles
    // =========================================================================

    private sealed class ThrowingHandler : ILocalEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent localEvent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("handler failure");
    }
}
