using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates background job naming conventions:
/// *Job suffix on IBackgroundJob implementors, [RecurringJob] requires IBackgroundJob.
/// </summary>
public sealed class BackgroundJobConventionTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = GranitArchitecture.Instance;

    [Fact]
    public void Background_jobs_must_end_with_Job() =>
        BackgroundJobConventionRules.BackgroundJobsMustEndWithJob(Architecture, "Granit.");

    [Fact]
    public void Recurring_jobs_must_implement_IBackgroundJob() =>
        BackgroundJobConventionRules.RecurringJobsMustImplementIBackgroundJob(Architecture, "Granit.");
}
