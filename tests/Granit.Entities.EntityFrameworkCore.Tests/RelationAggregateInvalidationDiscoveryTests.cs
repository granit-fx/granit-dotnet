using Granit.Domain;
using Granit.Entities;
using Granit.Entities.EntityFrameworkCore.Extensions;
using Granit.Entities.EntityFrameworkCore.Internal;
using Granit.Entities.Relations;
using Granit.Events;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Entities.EntityFrameworkCore.Tests;

public sealed class RelationAggregateInvalidationDiscoveryTests
{
    public sealed class Party { }
    public sealed class Account { }

    public sealed class Invoice : Entity, IEmitEntityLifecycleEvents { }

    public sealed class AuditLog { /* deliberately NOT IEmitEntityLifecycleEvents */ }

    private sealed class FakeDescriptor(string name, Type entityType, IReadOnlyList<RelationDescriptor> relations) : IEntityDefinitionDescriptor
    {
        public string Name => name;
        public Type EntityType => entityType;
        public EntityDefinitionDescriptor Descriptor { get; } = new()
        {
            Name = name,
            EntityType = entityType,
            Forms = [],
            Details = [],
            MetricDefinitionTypes = [],
            DashboardDefinitionTypes = [],
            Relations = relations,
        };
    }

    private static RelationDescriptor IntraRelation(string name, Type relatedClr) =>
        new(
            Name: name,
            Cardinality: RelationCardinality.Many,
            Display: RelationDisplay.Tab,
            TargetEntityName: relatedClr.FullName!,
            TargetEntityClrType: relatedClr,
            DisplayKey: null,
            Icon: null,
            Order: 0,
            RequiresPermission: null,
            ForeignKeyExpression: $"{name}-fk",
            Aggregates: [],
            QueryDefinitionName: null,
            ContributorAssemblyName: null);

    private sealed class AccountInvoiceContributor : IEntityRelationContributor
    {
        public void Contribute(IEntityRelationContributionContext context) =>
            context.AddRelation<Account, Invoice>(
                "invoices",
                "AccountInvoices",
                r => r.DisplayAs(RelationDisplay.SmartButton));
    }

    private sealed class UnknownSourceContributor : IEntityRelationContributor
    {
        // Source type not in the descriptor list — silent skip per merger semantics.
        private sealed class GhostSource { }
        public void Contribute(IEntityRelationContributionContext context) =>
            context.AddRelation<GhostSource, Invoice>(
                "invoices",
                "Ghost",
                r => r.DisplayAs(RelationDisplay.SmartButton));
    }

    [Fact]
    public void Scan_registers_invalidator_for_intra_module_related_type()
    {
        ServiceCollection services = [];
        services.AddSingleton<IEntityDefinitionDescriptor>(new FakeDescriptor(
            "Granit.Parties.Party", typeof(Party),
            [IntraRelation("invoices", typeof(Invoice))]));

        services.AddGranitEntitiesRelationAggregateInvalidation();

        Type targetsType = typeof(RelationAggregateInvalidationTargets<Invoice>);
        services.Any(sd => sd.ServiceType == targetsType).ShouldBeTrue();

        Type createdHandler = typeof(ILocalEventHandler<EntityCreatedEvent<Invoice>>);
        Type updatedHandler = typeof(ILocalEventHandler<EntityUpdatedEvent<Invoice>>);
        Type deletedHandler = typeof(ILocalEventHandler<EntityDeletedEvent<Invoice>>);
        Type bulkHandler = typeof(ILocalEventHandler<EntityBulkUpdatedEvent<Invoice>>);

        services.Any(sd => sd.ServiceType == createdHandler).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == updatedHandler).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == deletedHandler).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == bulkHandler).ShouldBeTrue();
    }

    [Fact]
    public void Scan_captures_per_relation_eviction_tag()
    {
        ServiceCollection services = [];
        services.AddSingleton<IEntityDefinitionDescriptor>(new FakeDescriptor(
            "Granit.Parties.Party", typeof(Party),
            [IntraRelation("invoices", typeof(Invoice))]));

        services.AddGranitEntitiesRelationAggregateInvalidation();

        using ServiceProvider provider = services.BuildServiceProvider();
        RelationAggregateInvalidationTargets<Invoice> targets =
            provider.GetRequiredService<RelationAggregateInvalidationTargets<Invoice>>();

        targets.EvictionTags.ShouldContain("entity-relation:Granit.Parties.Party:invoices");
    }

    [Fact]
    public void Scan_aggregates_tags_across_intra_and_cross_module_relations_for_same_related_type()
    {
        ServiceCollection services = [];
        services.AddSingleton<IEntityDefinitionDescriptor>(new FakeDescriptor(
            "Granit.Parties.Party", typeof(Party),
            [IntraRelation("invoices", typeof(Invoice))]));
        services.AddSingleton<IEntityDefinitionDescriptor>(new FakeDescriptor(
            "Granit.Sales.Account", typeof(Account),
            relations: []));
        services.AddSingleton<IEntityRelationContributor, AccountInvoiceContributor>();

        services.AddGranitEntitiesRelationAggregateInvalidation();

        using ServiceProvider provider = services.BuildServiceProvider();
        RelationAggregateInvalidationTargets<Invoice> targets =
            provider.GetRequiredService<RelationAggregateInvalidationTargets<Invoice>>();

        targets.EvictionTags.ShouldBe(
            ["entity-relation:Granit.Parties.Party:invoices",
             "entity-relation:Granit.Sales.Account:invoices"],
            ignoreOrder: true);
    }

    [Fact]
    public void Scan_silently_drops_contributions_for_unknown_source_types()
    {
        ServiceCollection services = [];
        services.AddSingleton<IEntityDefinitionDescriptor>(new FakeDescriptor(
            "Granit.Parties.Party", typeof(Party),
            [IntraRelation("invoices", typeof(Invoice))]));
        services.AddSingleton<IEntityRelationContributor, UnknownSourceContributor>();

        services.AddGranitEntitiesRelationAggregateInvalidation();

        using ServiceProvider provider = services.BuildServiceProvider();
        RelationAggregateInvalidationTargets<Invoice> targets =
            provider.GetRequiredService<RelationAggregateInvalidationTargets<Invoice>>();

        // Only the intra-module Party relation gets registered — the contribution
        // targeting an unknown source CLR type is silently skipped (mirrors the
        // EntityRelationMerger drop-with-debug-log semantic).
        targets.EvictionTags.ShouldBe(["entity-relation:Granit.Parties.Party:invoices"]);
    }

    [Fact]
    public void Scan_skips_related_types_that_do_not_emit_lifecycle_events()
    {
        ServiceCollection services = [];
        services.AddSingleton<IEntityDefinitionDescriptor>(new FakeDescriptor(
            "Granit.Audit.Source", typeof(Party),
            [IntraRelation("logs", typeof(AuditLog))]));

        services.AddGranitEntitiesRelationAggregateInvalidation();

        // No invalidator registered — AuditLog doesn't implement IEmitEntityLifecycleEvents,
        // so the ILocalEventHandler<EntityCreatedEvent<AuditLog>> generic constraint
        // would fail. Skip silently.
        services.Any(sd => sd.ImplementationType is { IsGenericType: true } it
            && it.GetGenericTypeDefinition() == typeof(RelationAggregateCacheInvalidator<>)
            && it.GetGenericArguments()[0] == typeof(AuditLog))
            .ShouldBeFalse();
    }

    [Fact]
    public void Scan_dedupes_duplicate_tags()
    {
        ServiceCollection services = [];
        // Same source declares the same relation twice (not realistic but guards against
        // accidental N invalidations on a single write).
        services.AddSingleton<IEntityDefinitionDescriptor>(new FakeDescriptor(
            "Granit.Parties.Party", typeof(Party),
            [
                IntraRelation("invoices", typeof(Invoice)),
                IntraRelation("invoices", typeof(Invoice)),
            ]));

        services.AddGranitEntitiesRelationAggregateInvalidation();

        using ServiceProvider provider = services.BuildServiceProvider();
        RelationAggregateInvalidationTargets<Invoice> targets =
            provider.GetRequiredService<RelationAggregateInvalidationTargets<Invoice>>();

        targets.EvictionTags.Count.ShouldBe(1);
    }
}
