// =============================================================================
// Tests - TraceContextBehavior (additional coverage)
// =============================================================================
// Covers MessageType null/empty edge case (no tag set) and ensures
// the bridge activity does not set the message_type tag when absent.
// =============================================================================

using System.Diagnostics;
using Granit.Wolverine.Behaviors;
using Granit.Wolverine.Middleware;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Wolverine.Tests;

[Collection("TraceContext")]
public sealed class TraceContextBehaviorAdditionalTests : IDisposable
{
    private const string ValidTraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    private readonly ILogger<TraceContextBehavior> _logger;
    private readonly List<Activity> _capturedActivities = [];
    private readonly ActivityListener _listener;

    public TraceContextBehaviorAdditionalTests()
    {
        _logger = Substitute.For<ILogger<TraceContextBehavior>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Granit.Wolverine",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _capturedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void Before_WithNullMessageType_DoesNotSetMessageTypeTag()
    {
        TraceContextBehavior behavior = new(_logger);
        Envelope envelope = new() { MessageType = null };
        envelope.Headers[OutgoingContextMiddleware.TraceParentHeader] = ValidTraceParent;

        behavior.Before(envelope);

        Activity started = _capturedActivities[0];
        started.GetTagItem("messaging.message_type").ShouldBeNull();

        behavior.After();
    }

    [Fact]
    public void Before_WithEmptyMessageType_DoesNotSetMessageTypeTag()
    {
        TraceContextBehavior behavior = new(_logger);
        Envelope envelope = new() { MessageType = string.Empty };
        envelope.Headers[OutgoingContextMiddleware.TraceParentHeader] = ValidTraceParent;

        behavior.Before(envelope);

        Activity started = _capturedActivities[0];
        started.GetTagItem("messaging.message_type").ShouldBeNull();

        behavior.After();
    }

    [Fact]
    public void Before_WithValidTraceParent_SetsMessagingSystemTag()
    {
        TraceContextBehavior behavior = new(_logger);
        Envelope envelope = new();
        envelope.Headers[OutgoingContextMiddleware.TraceParentHeader] = ValidTraceParent;

        behavior.Before(envelope);

        Activity started = _capturedActivities[0];
        started.GetTagItem("messaging.system").ShouldBe("wolverine");
        started.GetTagItem("messaging.operation").ShouldBe("process");

        behavior.After();
    }
}
