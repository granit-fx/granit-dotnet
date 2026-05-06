using Granit.Activities;
using Granit.Entities.Activities;
using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.Endpoints.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Entities.Endpoints.Tests;

public sealed class EntityActivitiesManifestTests
{
    private sealed class SampleEntity { }

    private static EntityDefinitionDescriptor DescriptorWithActivities(ActivitiesDescriptor? activities) => new()
    {
        Name = "Test.Sample",
        EntityType = typeof(SampleEntity),
        DisplayKey = "Entity:Sample",
        Icon = "box",
        PermissionGroup = "Test.Samples",
        DisplayProperty = "Name",
        QueryDefinitionType = null,
        ExportDefinitionType = null,
        MetricDefinitionTypes = [],
        DashboardDefinitionTypes = [],
        WorkflowDefinitionType = null,
        Forms = [],
        Details = [],
        Activities = activities,
    };

    private static IActivityRegistry RegistryWith(params string[] knownTypeNames)
    {
        IActivityRegistry registry = Substitute.For<IActivityRegistry>();
        Dictionary<string, ActivityType> dict = knownTypeNames.ToDictionary(
            name => name,
            name => new ActivityType(name, "icon", $"Activity:{name}"),
            StringComparer.Ordinal);
        registry.All.Returns(dict);
        foreach (string name in knownTypeNames)
        {
            ActivityType captured = dict[name];
            registry.TryGet(name, out Arg.Any<ActivityType?>()).Returns(call =>
            {
                call[1] = captured;
                return true;
            });
        }
        registry.TryGet(Arg.Is<string>(s => !knownTypeNames.Contains(s, StringComparer.Ordinal)), out Arg.Any<ActivityType?>())
            .Returns(false);
        return registry;
    }

    [Fact]
    public void Activities_section_omitted_when_descriptor_does_not_opt_in()
    {
        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            DescriptorWithActivities(activities: null),
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.All,
            defaultViewId: null,
            activityRegistry: RegistryWith("ToDo"));

        manifest.Activities.ShouldBeNull();
    }

    [Fact]
    public void Activities_section_omitted_when_registry_is_null_even_if_opted_in()
    {
        // Host did not load Granit.Activities runtime — silent-skip per ADR-045 §3.
        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            DescriptorWithActivities(new ActivitiesDescriptor(["ToDo"], null)),
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.All,
            defaultViewId: null,
            activityRegistry: null);

        manifest.Activities.ShouldBeNull();
    }

    [Fact]
    public void Activities_section_returns_full_registry_catalog_when_no_AllowedTypes_specified()
    {
        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            DescriptorWithActivities(new ActivitiesDescriptor([], DefaultAssigneePropertyName: null)),
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.All,
            defaultViewId: null,
            activityRegistry: RegistryWith("ToDo", "Call", "Meeting", "Email"));

        manifest.Activities.ShouldNotBeNull();
        manifest.Activities!.AllowedTypes.ShouldBe(["ToDo", "Call", "Meeting", "Email"], ignoreOrder: true);
        manifest.Activities.DefaultAssignee.ShouldBeNull();
    }

    [Fact]
    public void Activities_section_silently_drops_AllowedTypes_absent_from_registry()
    {
        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            DescriptorWithActivities(new ActivitiesDescriptor(["Call", "Quote", "Demo"], "AccountManagerUserId")),
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.All,
            defaultViewId: null,
            activityRegistry: RegistryWith("Call"));   // Quote and Demo not registered

        manifest.Activities!.AllowedTypes.ShouldBe(["Call"]);
        manifest.Activities.DefaultAssignee.ShouldBe("AccountManagerUserId");
    }

    [Fact]
    public void Activities_section_omitted_when_facet_not_requested()
    {
        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            DescriptorWithActivities(new ActivitiesDescriptor(["ToDo"], null)),
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Identity,   // Activities flag NOT included
            defaultViewId: null,
            activityRegistry: RegistryWith("ToDo"));

        manifest.Activities.ShouldBeNull();
    }
}
