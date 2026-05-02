using Granit.Domain;
using Granit.Entities.EntityFrameworkCore.Internal;
using Granit.Events;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Entities.EntityFrameworkCore.Tests;

public sealed class RelationAggregateCacheInvalidatorTests
{
    public sealed class SampleRelated : Entity, IEmitEntityLifecycleEvents
    {
    }

    [Fact]
    public async Task HandleAsync_Created_drops_every_captured_tag()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        RelationAggregateInvalidationTargets<SampleRelated> targets = new(
            ["entity-relation:Granit.Parties.Party:invoices",
             "entity-relation:Granit.Sales.Account:invoices"]);

        RelationAggregateCacheInvalidator<SampleRelated> sut = new(cache, targets);

        await sut.HandleAsync(new EntityCreatedEvent<SampleRelated>(new()), TestContext.Current.CancellationToken);

        await cache.Received(1).RemoveByTagAsync(
            "entity-relation:Granit.Parties.Party:invoices",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveByTagAsync(
            "entity-relation:Granit.Sales.Account:invoices",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Updated_drops_every_captured_tag()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        RelationAggregateInvalidationTargets<SampleRelated> targets = new(
            ["entity-relation:Granit.Parties.Party:invoices"]);

        RelationAggregateCacheInvalidator<SampleRelated> sut = new(cache, targets);

        await sut.HandleAsync(new EntityUpdatedEvent<SampleRelated>(new()), TestContext.Current.CancellationToken);

        await cache.Received(1).RemoveByTagAsync(
            "entity-relation:Granit.Parties.Party:invoices",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Deleted_drops_every_captured_tag()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        RelationAggregateInvalidationTargets<SampleRelated> targets = new(
            ["entity-relation:Granit.Parties.Party:invoices"]);

        RelationAggregateCacheInvalidator<SampleRelated> sut = new(cache, targets);

        await sut.HandleAsync(new EntityDeletedEvent<SampleRelated>(new()), TestContext.Current.CancellationToken);

        await cache.Received(1).RemoveByTagAsync(
            "entity-relation:Granit.Parties.Party:invoices",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_no_tags_captured_no_calls_to_cache()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        RelationAggregateInvalidationTargets<SampleRelated> targets = new([]);

        RelationAggregateCacheInvalidator<SampleRelated> sut = new(cache, targets);

        await sut.HandleAsync(new EntityCreatedEvent<SampleRelated>(new()), TestContext.Current.CancellationToken);

        await cache.DidNotReceive().RemoveByTagAsync(
            Arg.Any<string>(),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Targets_evictionTags_property_exposes_captured_list()
    {
        RelationAggregateInvalidationTargets<SampleRelated> sut = new(["a", "b"]);
        sut.EvictionTags.ShouldBe(["a", "b"]);
    }

    [Fact]
    public async Task HandleAsync_Bulk_drops_each_tag_once_regardless_of_batch_size()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        RelationAggregateInvalidationTargets<SampleRelated> targets = new(
            ["entity-relation:Granit.Parties.Party:invoices",
             "entity-relation:Granit.Sales.Account:invoices"]);

        RelationAggregateCacheInvalidator<SampleRelated> sut = new(cache, targets);

        // 100-row batch must collapse to 2 RemoveByTagAsync calls (one per tag),
        // not 200 — the whole point of EntityBulkUpdatedEvent.
        IReadOnlyList<SampleRelated> batch = [.. Enumerable.Range(0, 100).Select(_ => new SampleRelated())];
        await sut.HandleAsync(new EntityBulkUpdatedEvent<SampleRelated>(batch), TestContext.Current.CancellationToken);

        await cache.Received(1).RemoveByTagAsync(
            "entity-relation:Granit.Parties.Party:invoices",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveByTagAsync(
            "entity-relation:Granit.Sales.Account:invoices",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Bulk_empty_batch_is_a_no_op()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        RelationAggregateInvalidationTargets<SampleRelated> targets = new(
            ["entity-relation:Granit.Parties.Party:invoices"]);

        RelationAggregateCacheInvalidator<SampleRelated> sut = new(cache, targets);

        await sut.HandleAsync(new EntityBulkUpdatedEvent<SampleRelated>([]), TestContext.Current.CancellationToken);

        await cache.DidNotReceive().RemoveByTagAsync(
            Arg.Any<string>(),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }
}
