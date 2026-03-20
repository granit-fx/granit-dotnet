using System.Diagnostics;
using Granit.EventBus.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.EventBus.Tests.Diagnostics;

public sealed class EventBusActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;

    public EventBusActivitySourceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == EventBusActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void Name_is_Granit_EventBus() =>
        EventBusActivitySource.Name.ShouldBe("Granit.EventBus");

    [Fact]
    public void StartActivity_PublishLocal_returns_activity_when_listener_attached()
    {
        using Activity? activity = EventBusActivitySource.Source.StartActivity(EventBusActivitySource.PublishLocal);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("eventbus.publish.local");
    }

    [Fact]
    public void StartActivity_PublishDistributed_returns_activity_when_listener_attached()
    {
        using Activity? activity = EventBusActivitySource.Source.StartActivity(EventBusActivitySource.PublishDistributed);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("eventbus.publish.distributed");
    }
}
