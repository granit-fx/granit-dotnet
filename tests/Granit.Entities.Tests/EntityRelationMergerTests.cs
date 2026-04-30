using Granit.Entities.Internal;
using Granit.Entities.Relations;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Entities.Tests;

public sealed class EntityRelationMergerTests
{
    private sealed class Party { }
    private sealed class Invoice { }
    private sealed class Address { }

    [Fact]
    public void Merge_grafts_contribution_into_matching_source()
    {
        FakeDescriptor party = new("Test.Party", typeof(Party));
        FakeContributor invoicing = new(c => c.AddRelation<Party, Invoice>(
            "invoices",
            targetEntityName: "Test.Invoice",
            r => r.DisplayAs(RelationDisplay.SmartButton).Order(0)));

        IReadOnlyList<IEntityDefinitionDescriptor> merged =
            EntityRelationMerger.Merge([party], [invoicing], NullLogger.Instance);

        merged.ShouldHaveSingleItem();
        merged.Single().Descriptor.Relations.ShouldHaveSingleItem();
        merged.Single().Descriptor.Relations.Single().Name.ShouldBe("invoices");
        merged.Single().Descriptor.Relations.Single().ContributorAssemblyName.ShouldNotBeNull();
    }

    [Fact]
    public void Merge_drops_contribution_targeting_unknown_source()
    {
        FakeDescriptor party = new("Test.Party", typeof(Party));
        FakeContributor stray = new(c => c.AddRelation<Address, Invoice>(
            "shouldNotAppear", "Test.Invoice", r => { }));

        IReadOnlyList<IEntityDefinitionDescriptor> merged =
            EntityRelationMerger.Merge([party], [stray], NullLogger.Instance);

        merged.Single().Descriptor.Relations.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_intra_module_relation_takes_precedence_over_contribution_with_same_name()
    {
        // Intra-module declaration: a party has its own "invoices" relation
        EntityDefinitionDescriptor partyDescriptor = new()
        {
            Name = "Test.Party",
            EntityType = typeof(Party),
            MetricDefinitionTypes = [],
            DashboardDefinitionTypes = [],
            Forms = [],
            Details = [],
            Relations =
            [
                new RelationDescriptor(
                    "invoices",
                    RelationCardinality.Many,
                    RelationDisplay.Tab,
                    "Test.Invoice",
                    typeof(Invoice),
                    DisplayKey: "Owned",
                    Icon: null,
                    Order: 0,
                    RequiresPermission: null,
                    ForeignKeyExpression: "Party.Invoices",
                    Aggregates: [],
                    QueryDefinitionName: null,
                    ContributorAssemblyName: null),
            ],
        };
        FakeDescriptor host = new(partyDescriptor);

        FakeContributor contributor = new(c => c.AddRelation<Party, Invoice>(
            "invoices", "Test.Invoice", r => r.DisplayKey("Contributed")));

        IReadOnlyList<IEntityDefinitionDescriptor> merged =
            EntityRelationMerger.Merge([host], [contributor], NullLogger.Instance);

        RelationDescriptor only = merged.Single().Descriptor.Relations.Single();
        only.DisplayKey.ShouldBe("Owned");
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

    private sealed class FakeContributor(Action<IEntityRelationContributionContext> body)
        : IEntityRelationContributor
    {
        public void Contribute(IEntityRelationContributionContext context) => body(context);
    }
}
