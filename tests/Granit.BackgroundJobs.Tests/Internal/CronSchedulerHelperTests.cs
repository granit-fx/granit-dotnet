using Granit.BackgroundJobs.Internal;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests.Internal;

public sealed class CronSchedulerHelperTests
{
    [Fact]
    public void CreateMessage_KnownType_CreatesInstance()
    {
        string assemblyQualifiedName = typeof(FakeDailyReportMessage).AssemblyQualifiedName!;

        object result = CronSchedulerHelper.CreateMessage(assemblyQualifiedName, "test-job");

        result.ShouldNotBeNull();
        result.ShouldBeOfType<FakeDailyReportMessage>();
    }

    [Fact]
    public void CreateMessage_UnknownType_ThrowsInvalidOperationException()
    {
        Action act = () => CronSchedulerHelper.CreateMessage("Unknown.Type, UnknownAssembly", "test-job");

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(act);
        ex.Message.ShouldContain("Cannot resolve message type");
        ex.Message.ShouldContain("test-job");
    }
}
