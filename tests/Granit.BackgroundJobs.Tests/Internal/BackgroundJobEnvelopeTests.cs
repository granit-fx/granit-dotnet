using Granit.BackgroundJobs.Internal;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests.Internal;

public sealed class BackgroundJobEnvelopeTests
{
    [Fact]
    public void Constructor_SetsMessageProperty()
    {
        var message = new { Name = "test-job" };

        BackgroundJobEnvelope envelope = new(message);

        envelope.Message.ShouldBeSameAs(message);
    }

    [Fact]
    public void Constructor_HeadersDefaultsToNull()
    {
        BackgroundJobEnvelope envelope = new("job-message");

        envelope.Headers.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithHeaders_SetsHeadersProperty()
    {
        Dictionary<string, string> headers = new()
        {
            ["x-correlation-id"] = "abc-123",
            ["x-tenant-id"] = "tenant-1",
        };

        BackgroundJobEnvelope envelope = new("job-message", headers);

        envelope.Headers.ShouldNotBeNull();
        envelope.Headers!["x-correlation-id"].ShouldBe("abc-123");
        envelope.Headers["x-tenant-id"].ShouldBe("tenant-1");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        const string message = "same-message";
        BackgroundJobEnvelope a = new(message);
        BackgroundJobEnvelope b = new(message);

        a.ShouldBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentMessages_AreNotEqual()
    {
        BackgroundJobEnvelope a = new("message-a");
        BackgroundJobEnvelope b = new("message-b");

        a.ShouldNotBe(b);
    }
}
