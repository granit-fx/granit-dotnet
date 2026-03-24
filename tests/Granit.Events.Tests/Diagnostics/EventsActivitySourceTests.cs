using System.Diagnostics;
using Granit.Events.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Events.Tests.Diagnostics;

public sealed class EventsActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;

    public EventsActivitySourceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == EventsActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void Name_is_Granit_Events() =>
        EventsActivitySource.Name.ShouldBe("Granit.Events");

    [Fact]
    public void StartActivity_PublishLocal_returns_activity_when_listener_attached()
    {
        using Activity? activity = EventsActivitySource.Source.StartActivity(EventsActivitySource.PublishLocal);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("events.publish.local");
    }

    [Fact]
    public void StartActivity_PublishDistributed_returns_activity_when_listener_attached()
    {
        using Activity? activity = EventsActivitySource.Source.StartActivity(EventsActivitySource.PublishDistributed);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("events.publish.distributed");
    }
}
