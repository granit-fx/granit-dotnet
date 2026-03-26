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

    [Fact]
    public void CreateMessage_TypeNotImplementingIBackgroundJob_ThrowsInvalidOperationException()
    {
        string assemblyQualifiedName = typeof(NotABackgroundJob).AssemblyQualifiedName!;

        Action act = () => CronSchedulerHelper.CreateMessage(assemblyQualifiedName, "rogue-job");

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(act);
        ex.Message.ShouldContain("does not implement IBackgroundJob");
        ex.Message.ShouldContain("rogue-job");
    }

    // A valid CLR type that does NOT implement IBackgroundJob — simulates a tampered MessageType.
    private sealed class NotABackgroundJob;
}
