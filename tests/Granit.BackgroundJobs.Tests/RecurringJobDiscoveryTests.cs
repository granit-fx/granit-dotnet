using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Internal;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests;

public sealed class RecurringJobDiscoveryTests
{
    [Fact]
    public void Discover_TestAssembly_FindsDecoratedMessages()
    {
        // Act
        IReadOnlyList<RecurringJobRegistration> registrations =
            RecurringJobDiscovery.Discover([typeof(RecurringJobDiscoveryTests).Assembly]);

        // Assert — at least FakeDailyReport and FakeHourlyCleanup are present
        registrations.ShouldContain(r => r.JobName == "fake-daily-report");
        registrations.ShouldContain(r => r.JobName == "fake-hourly-cleanup");
    }

    [Fact]
    public void Discover_TestAssembly_PopulatesCronExpression()
    {
        // Act
        IReadOnlyList<RecurringJobRegistration> registrations =
            RecurringJobDiscovery.Discover([typeof(RecurringJobDiscoveryTests).Assembly]);

        // Assert
        RecurringJobRegistration reg = registrations.Single(r => r.JobName == "fake-daily-report");
        reg.CronExpression.ShouldBe("0 8 * * *");
    }

    [Fact]
    public void Discover_TestAssembly_PopulatesAssemblyQualifiedMessageType()
    {
        // Act
        IReadOnlyList<RecurringJobRegistration> registrations =
            RecurringJobDiscovery.Discover([typeof(RecurringJobDiscoveryTests).Assembly]);

        // Assert
        RecurringJobRegistration reg = registrations.Single(r => r.JobName == "fake-daily-report");
        reg.MessageType.ShouldContain(nameof(FakeDailyReportMessage));
        var resolved = Type.GetType(reg.MessageType);
        resolved.ShouldBe(typeof(FakeDailyReportMessage));
    }

    [Fact]
    public void Discover_UndecoratedMessageTypes_AreExcluded()
    {
        // Act
        IReadOnlyList<RecurringJobRegistration> registrations =
            RecurringJobDiscovery.Discover([typeof(RecurringJobDiscoveryTests).Assembly]);

        // Assert — UndecoratedMessage must NOT appear
        registrations.ShouldNotContain(r => r.JobName == "undecorated");
    }

    [Fact]
    public void Discover_EmptyAssemblyList_ReturnsEmpty()
    {
        // Act
        IReadOnlyList<RecurringJobRegistration> registrations =
            RecurringJobDiscovery.Discover([]);

        // Assert
        registrations.ShouldBeEmpty();
    }
}

// =========================================================================
// Test fixtures — message types used by RecurringJobDiscoveryTests
// =========================================================================

[RecurringJob("0 8 * * *", "fake-daily-report")]
public sealed class FakeDailyReportMessage : IBackgroundJob;

[RecurringJob("0 * * * *", "fake-hourly-cleanup")]
public sealed class FakeHourlyCleanupMessage : IBackgroundJob;

// Intentionally not decorated — must not appear in discovery results.
public sealed class UndecoratedMessage;
