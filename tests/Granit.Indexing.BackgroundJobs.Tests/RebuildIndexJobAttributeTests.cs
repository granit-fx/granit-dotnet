using System.Reflection;
using Granit.BackgroundJobs;
using Granit.Indexing.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.Indexing.BackgroundJobs.Tests;

public sealed class RebuildIndexJobAttributeTests
{
    [Fact]
    public void RebuildIndexJob_must_not_carry_RecurringJob_attribute()
    {
        // The job is on-demand. Decorating it with [RecurringJob] would crash any host
        // that runs RecurringJobDiscovery (it instantiates the attribute, whose ctor
        // rejects null/whitespace cron expressions) — caught the hard way in the
        // initial wiring of #2271. Guard against a regression.
        Type job = typeof(RebuildIndexJob<>);

        RecurringJobAttribute? attr = job.GetCustomAttribute<RecurringJobAttribute>();

        attr.ShouldBeNull(
            "RebuildIndexJob is dispatched on-demand — [RecurringJob] is for cron-driven jobs only.");
    }
}
