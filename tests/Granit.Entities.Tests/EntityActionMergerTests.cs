using Granit.Entities.Actions;
using Granit.Entities.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Entities.Tests;

public sealed class EntityActionMergerTests
{
    private sealed class Party { }
    private sealed class Address { }

    [Fact]
    public void Merge_grafts_contribution_into_matching_source()
    {
        FakeDescriptor party = new("Test.Party", typeof(Party));
        FakeContributor tasks = new(c => c.AddAction<Party>("add-task", a => a
            .ApiCall("POST", "/api/v1/parties/{id}/tasks")
            .DisplayKey("Tasks:Action.Add")
            .Order(0)));

        IReadOnlyList<IEntityDefinitionDescriptor> merged =
            EntityActionMerger.Merge([party], [tasks], NullLogger.Instance);

        merged.ShouldHaveSingleItem();
        EntityActionDescriptor only = merged.Single().Descriptor.Actions.ShouldHaveSingleItem();
        only.Name.ShouldBe("add-task");
        only.ContributorAssemblyName.ShouldNotBeNull();
    }

    [Fact]
    public void Merge_drops_contribution_targeting_unknown_source()
    {
        FakeDescriptor party = new("Test.Party", typeof(Party));
        FakeContributor stray = new(c => c.AddAction<Address>("add-task", a => a
            .ApiCall("POST", "/api/v1/addresses/{id}/tasks")));

        IReadOnlyList<IEntityDefinitionDescriptor> merged =
            EntityActionMerger.Merge([party], [stray], NullLogger.Instance);

        merged.Single().Descriptor.Actions.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_intra_module_action_takes_precedence_over_contribution_with_same_name()
    {
        EntityDefinitionDescriptor partyDescriptor = new()
        {
            Name = "Test.Party",
            EntityType = typeof(Party),
            MetricDefinitionTypes = [],
            DashboardDefinitionTypes = [],
            Forms = [],
            Details = [],
            Actions =
            [
                new EntityActionDescriptor(
                    Name: "merge",
                    Kind: EntityActionKind.Navigate,
                    DisplayKey: "Owned",
                    Icon: "git-merge",
                    Order: 5,
                    RequiresPermission: null,
                    UrlTemplate: "/parties/{id}/merge",
                    HttpMethod: null,
                    ConfirmationKey: null,
                    WorkflowTransitionName: null,
                    ContributorAssemblyName: null),
            ],
        };
        FakeDescriptor party = new(partyDescriptor);
        FakeContributor stray = new(c => c.AddAction<Party>("merge", a => a
            .Navigate("/contrib/parties/{id}/merge")
            .DisplayKey("Contributed")));

        IReadOnlyList<IEntityDefinitionDescriptor> merged =
            EntityActionMerger.Merge([party], [stray], NullLogger.Instance);

        EntityActionDescriptor only = merged.Single().Descriptor.Actions.ShouldHaveSingleItem();
        only.DisplayKey.ShouldBe("Owned");
        only.UrlTemplate.ShouldBe("/parties/{id}/merge");
    }

    [Fact]
    public void Merge_sorts_by_order_then_name()
    {
        FakeDescriptor party = new("Test.Party", typeof(Party));
        FakeContributor a = new(c => c
            .AddAction<Party>("zebra", x => x.ApiCall("POST", "/x").Order(5))
            .AddAction<Party>("alpha", x => x.ApiCall("POST", "/y").Order(5))
            .AddAction<Party>("first", x => x.ApiCall("POST", "/z").Order(1)));

        IReadOnlyList<IEntityDefinitionDescriptor> merged =
            EntityActionMerger.Merge([party], [a], NullLogger.Instance);

        merged.Single().Descriptor.Actions.Select(x => x.Name)
            .ShouldBe(["first", "alpha", "zebra"]);
    }

    private sealed class FakeDescriptor(EntityDefinitionDescriptor descriptor) : IEntityDefinitionDescriptor
    {
        public FakeDescriptor(string name, Type entityType) : this(new EntityDefinitionDescriptor
        {
            Name = name,
            EntityType = entityType,
            MetricDefinitionTypes = [],
            DashboardDefinitionTypes = [],
            Forms = [],
            Details = [],
        })
        { }

        public string Name => descriptor.Name;
        public Type EntityType => descriptor.EntityType;
        public EntityDefinitionDescriptor Descriptor => descriptor;
    }

    private sealed class FakeContributor(Action<IEntityActionContributionContext> body)
        : IEntityActionContributor
    {
        public void Contribute(IEntityActionContributionContext context) => body(context);
    }
}
