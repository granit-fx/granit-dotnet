using Granit.Authorization.Domain;
using Granit.Authorization.Events;
using Granit.Events;
using Granit.MultiTenancy;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests.Domain;

/// <summary>
/// Focused tests on the <see cref="RoleMetadata.MarkAsOrphaned"/> /
/// <see cref="RoleMetadata.RestoreFromOrphaned"/> behaviour introduced by ADR-029.
/// </summary>
public sealed class RoleMetadataOrphanTests
{
    private static RoleMetadata CreateSut() => RoleMetadata.Create(
        Guid.NewGuid(), "tester", MultiTenancySides.Host,
        tenantId: null, clientId: "client-a");

    [Fact]
    public void MarkAsOrphaned_WhenNotOrphaned_SetsFlag_StampsTimestamp_RaisesEvent()
    {
        RoleMetadata role = CreateSut();
        role.ClearDomainEvents();
        DateTimeOffset now = new(2026, 4, 23, 12, 0, 0, TimeSpan.Zero);

        role.MarkAsOrphaned(now);

        role.IsOrphaned.ShouldBeTrue();
        role.OrphanedAt.ShouldBe(now);

        IReadOnlyList<IDomainEvent> events = [.. role.DomainEvents];
        events.Count.ShouldBe(1);
        events[0].ShouldBeOfType<RoleOrphanedEvent>()
            .OrphanedAt.ShouldBe(now);
    }

    [Fact]
    public void MarkAsOrphaned_WhenAlreadyOrphaned_IsNoOp()
    {
        RoleMetadata role = CreateSut();
        DateTimeOffset first = new(2026, 4, 23, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset second = first.AddHours(1);

        role.MarkAsOrphaned(first);
        role.ClearDomainEvents();
        role.MarkAsOrphaned(second);

        role.OrphanedAt.ShouldBe(first, "second call must not overwrite the original timestamp");
        role.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void RestoreFromOrphaned_WhenOrphaned_ClearsFlagAndTimestamp_RaisesEvent()
    {
        RoleMetadata role = CreateSut();
        DateTimeOffset now = new(2026, 4, 23, 12, 0, 0, TimeSpan.Zero);
        role.MarkAsOrphaned(now);
        role.ClearDomainEvents();

        role.RestoreFromOrphaned();

        role.IsOrphaned.ShouldBeFalse();
        role.OrphanedAt.ShouldBeNull();
        role.DomainEvents.ShouldContain(e => e is RoleRestoredEvent);
    }

    [Fact]
    public void RestoreFromOrphaned_WhenNotOrphaned_IsNoOp()
    {
        RoleMetadata role = CreateSut();
        role.ClearDomainEvents();

        role.RestoreFromOrphaned();

        role.IsOrphaned.ShouldBeFalse();
        role.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void NewlyCreated_RoleIsNotOrphaned()
    {
        RoleMetadata role = CreateSut();

        role.IsOrphaned.ShouldBeFalse();
        role.OrphanedAt.ShouldBeNull();
    }
}
