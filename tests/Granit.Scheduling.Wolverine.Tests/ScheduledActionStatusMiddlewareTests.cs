using System.Diagnostics.Metrics;
using System.Reflection;
using Granit.MultiTenancy;
using Granit.Scheduling.Diagnostics;
using Granit.Scheduling.Domain;
using Granit.Scheduling.Domain.ValueObjects;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Scheduling.Wolverine.Tests;

/// <summary>
/// Tests for <see cref="ScheduledActionStatusMiddleware"/> covering header parsing, claim gating
/// (Before), executed/failed transitions (After/PostProcess), and metric emission.
/// </summary>
public sealed class ScheduledActionStatusMiddlewareTests : IDisposable
{
    private static readonly DateTimeOffset NowFixture = new(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly IScheduledActionReader _reader = Substitute.For<IScheduledActionReader>();
    private readonly IScheduledActionWriter _writer = Substitute.For<IScheduledActionWriter>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly SchedulingMetrics _metrics;

    public ScheduledActionStatusMiddlewareTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new SchedulingMetrics(_meterFactory);
        _clock.Now.Returns(NowFixture);
    }

    public void Dispose() => _sp.Dispose();

    private ScheduledActionStatusMiddleware CreateSut() =>
        new(_reader, _writer, _clock, _metrics, _currentTenant);

    private static Envelope CreateEnvelope(Guid? actionId = null)
    {
        Envelope envelope = new();
        if (actionId is not null)
        {
            envelope.Headers[ScheduledActionStatusMiddleware.ActionIdHeader] = actionId.Value.ToString();
        }
        return envelope;
    }

    private static Envelope CreateEnvelopeWithRawHeader(string headerValue)
    {
        Envelope envelope = new();
        envelope.Headers[ScheduledActionStatusMiddleware.ActionIdHeader] = headerValue;
        return envelope;
    }

    /// <summary>
    /// Builds a <see cref="ScheduledAction"/> directly in the requested status. The status setter
    /// is private and the Pending→Processing transition is performed by raw SQL in the EF store,
    /// so reflection is the only way to materialize a "Processing" aggregate in an isolated unit
    /// test of the middleware.
    /// </summary>
    private static ScheduledAction CreateActionInStatus(
        ScheduledActionStatus status,
        string payloadType = "Granit.Scheduling.Wolverine.Tests.FakePayload, Granit.Scheduling.Wolverine.Tests")
    {
        var action = ScheduledAction.Create(
            Guid.NewGuid(),
            payloadType,
            "{}",
            NowFixture.AddHours(1));

        PropertyInfo statusProp = typeof(ScheduledAction).GetProperty(
            nameof(ScheduledAction.Status),
            BindingFlags.Public | BindingFlags.Instance)!;
        statusProp.SetValue(action, status);

        return action;
    }

    // -------------------------------------------------------------------------
    // BeforeAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task BeforeAsync_ReturnsContinue_WhenHeaderMissing()
    {
        HandlerContinuation result = await CreateSut()
            .BeforeAsync(CreateEnvelope(), TestContext.Current.CancellationToken);

        result.ShouldBe(HandlerContinuation.Continue);
        await _writer.DidNotReceive().TryClaimForExecutionAsync(
            Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BeforeAsync_ReturnsContinue_WhenHeaderIsNotAGuid()
    {
        HandlerContinuation result = await CreateSut()
            .BeforeAsync(CreateEnvelopeWithRawHeader("not-a-guid"), TestContext.Current.CancellationToken);

        result.ShouldBe(HandlerContinuation.Continue);
        await _writer.DidNotReceive().TryClaimForExecutionAsync(
            Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BeforeAsync_ReturnsContinue_WhenWriterClaimsAction()
    {
        var actionId = Guid.NewGuid();
        _writer.TryClaimForExecutionAsync(Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>())
            .Returns(true);

        HandlerContinuation result = await CreateSut()
            .BeforeAsync(CreateEnvelope(actionId), TestContext.Current.CancellationToken);

        result.ShouldBe(HandlerContinuation.Continue);
        await _writer.Received(1).TryClaimForExecutionAsync(
            Arg.Is<ScheduledActionId>(id => id.Value == actionId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BeforeAsync_ReturnsStop_WhenWriterDoesNotClaim()
    {
        var actionId = Guid.NewGuid();
        _writer.TryClaimForExecutionAsync(Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>())
            .Returns(false);

        HandlerContinuation result = await CreateSut()
            .BeforeAsync(CreateEnvelope(actionId), TestContext.Current.CancellationToken);

        result.ShouldBe(HandlerContinuation.Stop);
    }

    // -------------------------------------------------------------------------
    // AfterAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AfterAsync_DoesNothing_WhenHeaderMissing()
    {
        await CreateSut().AfterAsync(CreateEnvelope(), TestContext.Current.CancellationToken);

        await _reader.DidNotReceive().GetByIdAsync(Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>());
        await _writer.DidNotReceive().UpdateAsync(Arg.Any<ScheduledAction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterAsync_DoesNothing_WhenActionNotFound()
    {
        var actionId = Guid.NewGuid();
        _reader.GetByIdAsync(Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>())
            .Returns((ScheduledAction?)null);

        await CreateSut().AfterAsync(CreateEnvelope(actionId), TestContext.Current.CancellationToken);

        await _writer.DidNotReceive().UpdateAsync(Arg.Any<ScheduledAction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterAsync_DoesNothing_WhenActionNotInProcessing()
    {
        var actionId = Guid.NewGuid();
        ScheduledAction action = CreateActionInStatus(ScheduledActionStatus.Pending);
        _reader.GetByIdAsync(Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>())
            .Returns(action);

        await CreateSut().AfterAsync(CreateEnvelope(actionId), TestContext.Current.CancellationToken);

        await _writer.DidNotReceive().UpdateAsync(Arg.Any<ScheduledAction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterAsync_MarksExecutedAndEmitsMetric_WhenActionIsProcessing()
    {
        var actionId = Guid.NewGuid();
        ScheduledAction action = CreateActionInStatus(ScheduledActionStatus.Processing);
        _reader.GetByIdAsync(Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>())
            .Returns(action);
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        using MetricCollector<long> collector = new(
            _meterFactory, SchedulingMetrics.MeterName, "granit.scheduling.action.executed");

        await CreateSut().AfterAsync(CreateEnvelope(actionId), TestContext.Current.CancellationToken);

        action.Status.ShouldBe(ScheduledActionStatus.Executed);
        action.ExecutedAt.ShouldBe(NowFixture);
        await _writer.Received(1).UpdateAsync(action, Arg.Any<CancellationToken>());

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.Count.ShouldBe(1);
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["tenant_id"].ShouldBe("11111111-1111-1111-1111-111111111111");
        // PayloadType "FullName, Assembly" → last segment of the FullName part.
        snapshot[0].Tags["payload_type"].ShouldBe("FakePayload");
    }

    [Fact]
    public async Task AfterAsync_TagsTenantAsGlobal_WhenNoCurrentTenant()
    {
        var actionId = Guid.NewGuid();
        ScheduledAction action = CreateActionInStatus(ScheduledActionStatus.Processing);
        _reader.GetByIdAsync(Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>())
            .Returns(action);
        _currentTenant.IsAvailable.Returns(false);

        using MetricCollector<long> collector = new(
            _meterFactory, SchedulingMetrics.MeterName, "granit.scheduling.action.executed");

        await CreateSut().AfterAsync(CreateEnvelope(actionId), TestContext.Current.CancellationToken);

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot[0].Tags["tenant_id"].ShouldBe("global");
    }

    // -------------------------------------------------------------------------
    // PostProcessAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostProcessAsync_DoesNothing_WhenHeaderMissing()
    {
        await CreateSut().PostProcessAsync(
            CreateEnvelope(), new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

        await _reader.DidNotReceive().GetByIdAsync(Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>());
        await _writer.DidNotReceive().UpdateAsync(Arg.Any<ScheduledAction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostProcessAsync_DoesNothing_WhenActionNotFound()
    {
        var actionId = Guid.NewGuid();
        _reader.GetByIdAsync(Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>())
            .Returns((ScheduledAction?)null);

        await CreateSut().PostProcessAsync(
            CreateEnvelope(actionId), new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

        await _writer.DidNotReceive().UpdateAsync(Arg.Any<ScheduledAction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostProcessAsync_DoesNothing_WhenActionNotInProcessing()
    {
        var actionId = Guid.NewGuid();
        ScheduledAction action = CreateActionInStatus(ScheduledActionStatus.Executed);
        _reader.GetByIdAsync(Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>())
            .Returns(action);

        await CreateSut().PostProcessAsync(
            CreateEnvelope(actionId), new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

        await _writer.DidNotReceive().UpdateAsync(Arg.Any<ScheduledAction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostProcessAsync_MarksFailedAndEmitsMetric_WhenActionIsProcessing()
    {
        var actionId = Guid.NewGuid();
        ScheduledAction action = CreateActionInStatus(ScheduledActionStatus.Processing);
        _reader.GetByIdAsync(Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>())
            .Returns(action);
        _currentTenant.IsAvailable.Returns(false);

        using MetricCollector<long> collector = new(
            _meterFactory, SchedulingMetrics.MeterName, "granit.scheduling.action.failed");

        var exception = new InvalidOperationException("payload handler exploded");
        await CreateSut().PostProcessAsync(
            CreateEnvelope(actionId), exception, TestContext.Current.CancellationToken);

        action.Status.ShouldBe(ScheduledActionStatus.Failed);
        action.FailureReason.ShouldBe("payload handler exploded");
        action.ExecutedAt.ShouldBe(NowFixture);
        await _writer.Received(1).UpdateAsync(action, Arg.Any<CancellationToken>());

        IReadOnlyList<CollectedMeasurement<long>> snapshot = collector.GetMeasurementSnapshot();
        snapshot.Count.ShouldBe(1);
        snapshot[0].Value.ShouldBe(1);
        snapshot[0].Tags["payload_type"].ShouldBe("FakePayload");
    }
}
