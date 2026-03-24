// =============================================================================
// Tests - GranitNotificationsModule
// =============================================================================
// Verifies the module metadata: DependsOn attributes ensure the correct
// module dependency graph for Timing prerequisites.
// =============================================================================

using Granit.Modularity;
using Granit.Timing;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class GranitNotificationsModuleTests
{
    [Fact]
    public void Module_DependsOnGranitTimingModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitNotificationsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitTimingModule)));
    }

    [Fact]
    public void Module_IsGranitModule()
    {
        GranitNotificationsModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
