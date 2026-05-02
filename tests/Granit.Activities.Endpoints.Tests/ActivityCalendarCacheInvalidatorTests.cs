using Granit.Activities.Abstractions;
using Granit.Activities.Domain;
using Granit.Activities.Endpoints.Internal;
using Granit.Events;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Activities.Endpoints.Tests;

public sealed class ActivityCalendarCacheInvalidatorTests
{
    private static IActivityRegistry RegistryWithToDo()
    {
        IActivityRegistry registry = Substitute.For<IActivityRegistry>();
        registry.TryGet("ToDo", out Arg.Any<ActivityType>()).Returns(call =>
        {
            call[1] = StandardActivityTypes.ToDo;
            return true;
        });
        return registry;
    }

    private static Activity NewActivity(Guid? tenantId)
    {
        var a = Activity.Create(
            id: Guid.NewGuid(),
            entityType: "Granit.Parties.Party",
            entityId: Guid.NewGuid(),
            type: "ToDo",
            assignedToUserId: Guid.NewGuid(),
            dueAt: DateTimeOffset.UtcNow.AddDays(1),
            registry: RegistryWithToDo(),
            tenantId: tenantId);
        return a;
    }

    [Fact]
    public async Task Created_event_drops_per_tenant_eviction_tag()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        ActivityCalendarCacheInvalidator sut = new(cache);
        var tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

        await sut.HandleAsync(new EntityCreatedEvent<Activity>(NewActivity(tenant)),
            TestContext.Current.CancellationToken);

        await cache.Received(1).RemoveByTagAsync(
            $"activity-calendar:tenant:{tenant}",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Updated_event_drops_per_tenant_eviction_tag()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        ActivityCalendarCacheInvalidator sut = new(cache);
        var tenant = Guid.Parse("22222222-2222-2222-2222-222222222222");

        await sut.HandleAsync(new EntityUpdatedEvent<Activity>(NewActivity(tenant)),
            TestContext.Current.CancellationToken);

        await cache.Received(1).RemoveByTagAsync(
            $"activity-calendar:tenant:{tenant}",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deleted_event_drops_per_tenant_eviction_tag()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        ActivityCalendarCacheInvalidator sut = new(cache);

        await sut.HandleAsync(new EntityDeletedEvent<Activity>(NewActivity(tenantId: null)),
            TestContext.Current.CancellationToken);

        await cache.Received(1).RemoveByTagAsync(
            "activity-calendar:tenant:global",
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Bulk_event_collapses_to_one_call_per_distinct_tenant()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        ActivityCalendarCacheInvalidator sut = new(cache);
        var t1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var t2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        // 100 rows across two tenants — one RemoveByTagAsync per tenant.
        IReadOnlyList<Activity> batch =
        [
            .. Enumerable.Range(0, 50).Select(_ => NewActivity(t1)),
            .. Enumerable.Range(0, 50).Select(_ => NewActivity(t2)),
        ];

        await sut.HandleAsync(new EntityBulkUpdatedEvent<Activity>(batch),
            TestContext.Current.CancellationToken);

        await cache.Received(1).RemoveByTagAsync($"activity-calendar:tenant:{t1}",
            Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveByTagAsync($"activity-calendar:tenant:{t2}",
            Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Bulk_event_with_empty_batch_is_a_no_op()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        ActivityCalendarCacheInvalidator sut = new(cache);

        await sut.HandleAsync(new EntityBulkUpdatedEvent<Activity>([]),
            TestContext.Current.CancellationToken);

        await cache.DidNotReceive().RemoveByTagAsync(
            Arg.Any<string>(), Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }
}
