using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Scheduling.Diagnostics;
using Granit.Scheduling.Domain;
using Granit.Scheduling.Domain.ValueObjects;
using Granit.Scheduling.Wolverine.Internal;
using Granit.Users;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Scheduling.Wolverine.Tests;

public sealed class WolverineSchedulerTests
{
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly IScheduledActionReader _reader = Substitute.For<IScheduledActionReader>();
    private readonly IScheduledActionWriter _writer = Substitute.For<IScheduledActionWriter>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly ScheduledPayloadTypeRegistry _typeRegistry = new();
    private readonly SchedulingMetrics _metrics;
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    public WolverineSchedulerTests()
    {
        var meterFactory = new TestMeterFactory();
        _metrics = new SchedulingMetrics(meterFactory);
    }

    private WolverineScheduler CreateSut() => new(
        _messageBus, _reader, _writer, _guidGenerator,
        _typeRegistry, _metrics, _currentUserService, _currentTenant);

    [Fact]
    public async Task ScheduleAsync_ShouldPersistAndPublish()
    {
        var id = Guid.NewGuid();
        _guidGenerator.Create().Returns(id);

        var payload = new TestPayload("test");
        DateTimeOffset executeAt = DateTimeOffset.UtcNow.AddHours(1);

        ScheduledActionId result = await CreateSut()
            .ScheduleAsync(payload, executeAt, cancellationToken: TestContext.Current.CancellationToken);

        result.Value.ShouldBe(id);
        await _writer.Received(1).AddAsync(
            Arg.Is<ScheduledAction>(a => a.Id == id),
            Arg.Any<CancellationToken>());
        await _messageBus.Received(1).PublishAsync(
            payload,
            Arg.Any<DeliveryOptions>());
    }

    [Fact]
    public async Task CancelAsync_WhenNotFound_ShouldThrow()
    {
        _reader.GetByIdAsync(Arg.Any<ScheduledActionId>(), Arg.Any<CancellationToken>())
            .Returns((ScheduledAction?)null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => CreateSut().CancelAsync(
                ScheduledActionId.Create(Guid.NewGuid()),
                TestContext.Current.CancellationToken));
    }

    private sealed record TestPayload(string Value) : IScheduledPayload;

    private sealed class TestMeterFactory : System.Diagnostics.Metrics.IMeterFactory
    {
        public System.Diagnostics.Metrics.Meter Create(System.Diagnostics.Metrics.MeterOptions options) =>
            new(options);

        public void Dispose() { }
    }
}
