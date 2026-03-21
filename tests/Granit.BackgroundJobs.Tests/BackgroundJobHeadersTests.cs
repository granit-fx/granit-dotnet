using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests;

public sealed class BackgroundJobHeadersTests
{
    [Fact]
    public void TriggeredBy_HasExpectedValue() => BackgroundJobHeaders.TriggeredBy.ShouldBe("X-Triggered-By");
}
