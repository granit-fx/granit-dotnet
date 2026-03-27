// =============================================================================
// Tests - InProcessDistributedEventBus (Behavioral)
// =============================================================================
// Covers behavioral scenarios not addressed by the base and additional tests:
//   - Cancellation token is forwarded to handlers
//   - Handler execution order matches DI registration order
//   - Concurrent publishing is safe
//   - Durability warning is logged only once across multiple publishes
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Events.Diagnostics;
using Granit.Events.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Events.Tests;

public sealed class InProcessDistributedEventBusBehaviorTests : IDisposable
{
    // =========================================================================
    // Test doubles
    // =========================================================================

    private sealed record TestIntegrationEvent(string Value) : IIntegrationEvent;

    private sealed class TokenCapturingHandler : IDistributedEventHandler<TestIntegrationEvent>
    {
        public CancellationToken CapturedToken { get; private set; }

        public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            CapturedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class OrderTrackingHandler(List<int> executionOrder, int id)
        : IDistributedEventHandler<TestIntegrationEvent>
    {
        public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            executionOrder.Add(id);
            return Task.CompletedTask;
        }
    }

    private sealed class CountingHandler : IDistributedEventHandler<TestIntegrationEvent>
    {
        private int _callCount;

        public int CallCount => _callCount;

        public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
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

    public InProcessDistributedEventBusBehaviorTests()
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
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(handler);
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessDistributedEventBus bus = new(
            sp, NullLogger<InProcessDistributedEventBus>.Instance, _metrics);

        using CancellationTokenSource cts = new();
        CancellationToken token = cts.Token;

        // Act
        await bus.PublishAsync(new TestIntegrationEvent("token-test"), token);

        // Assert
        handler.CapturedToken.ShouldBe(token);
    }

    [Fact]
    public async Task PublishAsync_DefaultCancellationToken_ForwardedAsNone()
    {
        // Arrange
        TokenCapturingHandler handler = new();
        ServiceCollection services = new();
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(handler);
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessDistributedEventBus bus = new(
            sp, NullLogger<InProcessDistributedEventBus>.Instance, _metrics);

        // Act — intentionally omitting CancellationToken to verify default propagation
#pragma warning disable xUnit1051
        await bus.PublishAsync(new TestIntegrationEvent("default-token"));
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
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(
            new OrderTrackingHandler(executionOrder, 1));
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(
            new OrderTrackingHandler(executionOrder, 2));
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(
            new OrderTrackingHandler(executionOrder, 3));
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessDistributedEventBus bus = new(
            sp, NullLogger<InProcessDistributedEventBus>.Instance, _metrics);

        // Act
        await bus.PublishAsync(
            new TestIntegrationEvent("order-test"), TestContext.Current.CancellationToken);

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
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(handler);
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessDistributedEventBus bus = new(
            sp, NullLogger<InProcessDistributedEventBus>.Instance, _metrics);

        const int concurrentPublishes = 50;

        // Act — fire multiple publishes concurrently
        Task[] tasks = Enumerable.Range(0, concurrentPublishes)
            .Select(i => bus.PublishAsync(
                new TestIntegrationEvent($"concurrent-{i}"), TestContext.Current.CancellationToken))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert
        handler.CallCount.ShouldBe(concurrentPublishes);
    }

    // =========================================================================
    // Durability warning (logged once)
    // =========================================================================

    [Fact]
    public async Task PublishAsync_FirstCall_LogsDurabilityWarning()
    {
        // Arrange
        ILogger<InProcessDistributedEventBus> logger = Substitute.For<ILogger<InProcessDistributedEventBus>>();
        logger.IsEnabled(LogLevel.Warning).Returns(true);
        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();

        InProcessDistributedEventBus bus = new(sp, logger, _metrics);

        // Act
        await bus.PublishAsync(
            new TestIntegrationEvent("first"), TestContext.Current.CancellationToken);

        // Assert — the generated [LoggerMessage] calls Log<TState> with a struct state type;
        // ReceivedWithAnyArgs matches regardless of generic type parameter
        logger.ReceivedWithAnyArgs().Log(
            default,
            default,
            default(object),
            default,
            default(Func<object, Exception?, string>)!);
    }

    [Fact]
    public async Task PublishAsync_MultipleCalls_LogsDurabilityWarningOnlyOnce()
    {
        // Arrange
        WarningCountingLogger logger = new();
        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();

        InProcessDistributedEventBus bus = new(sp, logger, _metrics);

        // Act
        await bus.PublishAsync(
            new TestIntegrationEvent("first"), TestContext.Current.CancellationToken);
        await bus.PublishAsync(
            new TestIntegrationEvent("second"), TestContext.Current.CancellationToken);
        await bus.PublishAsync(
            new TestIntegrationEvent("third"), TestContext.Current.CancellationToken);

        // Assert — warning logged exactly once despite three publishes
        logger.WarningCount.ShouldBe(1);
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
        services.AddSingleton<IDistributedEventHandler<TestIntegrationEvent>>(new ThrowingHandler());
        ServiceProvider sp = services.BuildServiceProvider();

        InProcessDistributedEventBus bus = new(
            sp, NullLogger<InProcessDistributedEventBus>.Instance, _metrics);

        // Act
        await bus.PublishAsync(
            new TestIntegrationEvent("metrics-test"), TestContext.Current.CancellationToken);

        // Assert — published metric recorded regardless of handler failure
        IReadOnlyList<CollectedMeasurement<long>> published = publishedCollector.GetMeasurementSnapshot();
        published.ShouldHaveSingleItem();
        published[0].Tags["bus_type"].ShouldBe("distributed");
        published[0].Tags["event_type"].ShouldBe("TestIntegrationEvent");

        // Assert — handler error metric recorded
        IReadOnlyList<CollectedMeasurement<long>> handlers = handlerCollector.GetMeasurementSnapshot();
        handlers.ShouldHaveSingleItem();
        handlers[0].Tags["status"].ShouldBe("error");
    }

    // =========================================================================
    // Shared test doubles
    // =========================================================================

    private sealed class ThrowingHandler : IDistributedEventHandler<TestIntegrationEvent>
    {
        public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("handler failure");
    }

    /// <summary>
    /// Minimal logger that counts Warning-level log calls.
    /// Avoids NSubstitute generic type matching issues with [LoggerMessage]-generated code.
    /// </summary>
    private sealed class WarningCountingLogger : ILogger<InProcessDistributedEventBus>
    {
        private int _warningCount;

        public int WarningCount => _warningCount;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Interlocked.Increment(ref _warningCount);
            }
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
