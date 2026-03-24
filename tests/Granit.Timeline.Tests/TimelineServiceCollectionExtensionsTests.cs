// =============================================================================
// Tests — TimelineServiceCollectionExtensions
// =============================================================================
// Verifies that AddGranitTimeline registers all expected services.
// =============================================================================

using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Extensions;
using Granit.Timing;
using Granit.Users;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitTimeline_RegistersTimelineWriter()
    {
        ServiceCollection services = new();
        AddRequiredDependencies(services);

        services.AddGranitTimeline();

        using ServiceProvider sp = services.BuildServiceProvider();
        ITimelineWriter? writer = sp.GetService<ITimelineWriter>();
        writer.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitTimeline_RegistersTimelineReader()
    {
        ServiceCollection services = new();
        AddRequiredDependencies(services);

        services.AddGranitTimeline();

        using ServiceProvider sp = services.BuildServiceProvider();
        ITimelineReader? reader = sp.GetService<ITimelineReader>();
        reader.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitTimeline_RegistersTimelineFollowerService()
    {
        ServiceCollection services = new();
        AddRequiredDependencies(services);

        services.AddGranitTimeline();

        using ServiceProvider sp = services.BuildServiceProvider();
        ITimelineFollowerService? followerService = sp.GetService<ITimelineFollowerService>();
        followerService.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitTimeline_RegistersTimelineNotifier()
    {
        ServiceCollection services = new();
        AddRequiredDependencies(services);

        services.AddGranitTimeline();

        using ServiceProvider sp = services.BuildServiceProvider();
        ITimelineNotifier? notifier = sp.GetService<ITimelineNotifier>();
        notifier.ShouldNotBeNull();
    }

    private static void AddRequiredDependencies(ServiceCollection services)
    {
        services.AddSingleton(Substitute.For<IClock>());
        services.AddSingleton(Substitute.For<IGuidGenerator>());
        services.AddSingleton(Substitute.For<ICurrentUserService>());
        services.AddSingleton(Substitute.For<ICurrentTenant>());
    }
}
