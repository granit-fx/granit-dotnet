// =============================================================================
// Tests — AuditingTimelineSource
// =============================================================================
// Verifies projection from AuditEntry to TimelineStreamEntry and the security
// check on GetEntryAsync (must refuse audit rows that don't target the
// claimed entity).
// =============================================================================

using System.Text.Json;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.QueryEngine;
using Granit.Timeline.Auditing.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Auditing.Tests;

public sealed class AuditingTimelineSourceTests
{
    private readonly IAuditingReader _auditing = Substitute.For<IAuditingReader>();
    private readonly AuditingTimelineSource _source;

    public AuditingTimelineSourceTests() => _source = new AuditingTimelineSource(_auditing, [], []);

    private static AuditingTimelineSource WithUserAlias(IAuditingReader auditing) =>
        new(auditing, [
            new StaticAuditEntityTypeAliasProvider(
                new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
                {
                    ["User"] = new HashSet<string>(StringComparer.Ordinal) { "LocalIdentity", "FederatedIdentity" },
                }),
        ], []);

    [Fact]
    public void SourceKey_IsAuditing() => _source.SourceKey.ShouldBe("auditing");

    [Fact]
    public async Task GetEntriesAsync_ProjectsAuditEntryToExternalSystemLog()
    {
        var auditId = Guid.NewGuid();
        var entry = new AuditEntry
        {
            Id = auditId,
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "u-1",
            UserName = "Alice",
            Category = AuditCategory.DataMutation,
            EntityChanges = [new AuditEntityChange
            {
                EntityType = "User",
                EntityId = "user-42",
                ChangeType = AuditChangeType.Modified,
                PropertyChanges = [new AuditPropertyChange
                {
                    PropertyName = "Email",
                    OriginalValue = "old@example.com",
                    NewValue = "new@example.com",
                }],
            }],
        };

        _auditing.GetByEntityAsync("User", "user-42", 1, 50, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditEntry>([entry], 1, false));

        IReadOnlyList<TimelineStreamEntry> result = await _source
            .GetEntriesAsync("User", "user-42", 50, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        TimelineStreamEntry projected = result[0];
        projected.Origin.ShouldBe(TimelineEntryOrigin.External);
        projected.SourceKey.ShouldBe("auditing");
        projected.SourceId.ShouldBe(auditId.ToString("D"));
        projected.EntryType.ShouldBe(TimelineStreamEntryType.SystemLog);
        projected.AuthorId.ShouldBe("u-1");
        projected.AuthorName.ShouldBe("Alice");

        using var payload = JsonDocument.Parse(projected.Body);
        payload.RootElement.GetProperty("category").GetString().ShouldBe("DataMutation");
        payload.RootElement.GetProperty("changeType").GetString().ShouldBe("Modified");
        JsonElement change = payload.RootElement.GetProperty("changes")[0];
        change.GetProperty("property").GetString().ShouldBe("Email");
        change.GetProperty("from").GetString().ShouldBe("old@example.com");
        change.GetProperty("to").GetString().ShouldBe("new@example.com");
    }

    [Fact]
    public async Task GetEntryAsync_ReturnsNull_WhenAuditEntryDoesNotTargetClaimedEntity()
    {
        var auditId = Guid.NewGuid();
        var entry = new AuditEntry
        {
            Id = auditId,
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "u-1",
            EntityChanges = [new AuditEntityChange
            {
                EntityType = "Invoice", // different entity type!
                EntityId = "user-42",
                ChangeType = AuditChangeType.Modified,
            }],
        };

        _auditing.GetByIdAsync(auditId, Arg.Any<CancellationToken>()).Returns(entry);

        TimelineStreamEntry? result = await _source
            .GetEntryAsync("User", "user-42", auditId.ToString(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetEntryAsync_ReturnsNull_WhenSourceIdIsNotAGuid()
    {
        TimelineStreamEntry? result = await _source
            .GetEntryAsync("User", "user-42", "not-a-guid", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Alias resolution — ADR-051 split persistence
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetEntriesAsync_WithAliasProvider_ProjectsEntriesStampedWithAliasedClrName()
    {
        // Audit row stamped "LocalIdentity" (auth-secret mutation), queried
        // via the canonical "User" endpoint — must surface and project the
        // matching change.
        var auditId = Guid.NewGuid();
        var entry = new AuditEntry
        {
            Id = auditId,
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "u-1",
            UserName = "Alice",
            Category = AuditCategory.DataMutation,
            EntityChanges = [new AuditEntityChange
            {
                EntityType = "LocalIdentity",
                EntityId = "user-42",
                ChangeType = AuditChangeType.Modified,
                PropertyChanges = [new AuditPropertyChange
                {
                    PropertyName = "PasswordHash",
                    OriginalValue = "***",
                    NewValue = "***",
                }],
            }],
        };

        _auditing.GetByEntityAsync("User", "user-42", 1, 50, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditEntry>([entry], 1, false));

        AuditingTimelineSource source = WithUserAlias(_auditing);

        IReadOnlyList<TimelineStreamEntry> result = await source
            .GetEntriesAsync("User", "user-42", 50, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        using var payload = JsonDocument.Parse(result[0].Body);
        payload.RootElement.GetProperty("changeType").GetString().ShouldBe("Modified");
        payload.RootElement.GetProperty("changes")[0].GetProperty("property").GetString().ShouldBe("PasswordHash");
    }

    [Fact]
    public async Task GetEntryAsync_WithAliasProvider_AcceptsAuditRowStampedWithAliasedClrName()
    {
        // Security check must accept aliased CLR names — otherwise the entry
        // anchored from /timeline/User/{id} would be rejected.
        var auditId = Guid.NewGuid();
        var entry = new AuditEntry
        {
            Id = auditId,
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "u-1",
            EntityChanges = [new AuditEntityChange
            {
                EntityType = "FederatedIdentity",
                EntityId = "user-42",
                ChangeType = AuditChangeType.Modified,
            }],
        };

        _auditing.GetByIdAsync(auditId, Arg.Any<CancellationToken>()).Returns(entry);

        AuditingTimelineSource source = WithUserAlias(_auditing);

        TimelineStreamEntry? result = await source
            .GetEntryAsync("User", "user-42", auditId.ToString(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.SourceId.ShouldBe(auditId.ToString("D"));
    }

    [Fact]
    public async Task GetEntryAsync_QueriedByPhysicalAlias_DoesNotMatchCanonicalRow()
    {
        // Directional alias: forensic lookup by "LocalIdentity" must NOT
        // accept rows stamped "User".
        var auditId = Guid.NewGuid();
        var entry = new AuditEntry
        {
            Id = auditId,
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "u-1",
            EntityChanges = [new AuditEntityChange
            {
                EntityType = "User",
                EntityId = "user-42",
                ChangeType = AuditChangeType.Modified,
            }],
        };

        _auditing.GetByIdAsync(auditId, Arg.Any<CancellationToken>()).Returns(entry);

        AuditingTimelineSource source = WithUserAlias(_auditing);

        TimelineStreamEntry? result = await source
            .GetEntryAsync("LocalIdentity", "user-42", auditId.ToString(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Parent-child aggregation — IAuditChildResolver
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetEntriesAsync_NoResolvers_UsesCachedSingleEntityPath()
    {
        // No resolver registered → fast path goes through GetByEntityAsync
        // (which is FusionCache-backed). The batch overload must NOT be hit.
        var entry = new AuditEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "u-1",
            EntityChanges = [new AuditEntityChange { EntityType = "Page", EntityId = "page-1", ChangeType = AuditChangeType.Modified }],
        };
        _auditing.GetByEntityAsync("Page", "page-1", 1, 50, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditEntry>([entry], 1, false));

        IReadOnlyList<TimelineStreamEntry> result = await _source
            .GetEntriesAsync("Page", "page-1", 50, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        await _auditing.DidNotReceive().GetByEntitiesAsync(
            Arg.Any<IReadOnlyCollection<AuditEntityRef>>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEntriesAsync_WithChildResolver_UnionsParentAndChildAudits()
    {
        // Parent + 2 children — verifies (a) batch overload is invoked,
        // (b) target set assembled correctly, (c) every returned entry projects.
        AuditEntry parentEntry = MakeEntry("Page", "page-1", AuditChangeType.Modified);
        AuditEntry version1Entry = MakeEntry("PageVersion", "v1", AuditChangeType.Created);
        AuditEntry version2Entry = MakeEntry("PageVersion", "v2", AuditChangeType.Modified);

        _auditing.GetByEntitiesAsync(
                Arg.Any<IReadOnlyCollection<AuditEntityRef>>(),
                50,
                Arg.Any<CancellationToken>())
            .Returns([parentEntry, version1Entry, version2Entry]);

        FakeChildResolver resolver = new("Page", "page-1",
            [new AuditChildScope("PageVersion", ["v1", "v2"])]);
        AuditingTimelineSource source = new(_auditing, [], [resolver]);

        IReadOnlyList<TimelineStreamEntry> result = await source
            .GetEntriesAsync("Page", "page-1", 50, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(3);
        await _auditing.Received(1).GetByEntitiesAsync(
            Arg.Is<IReadOnlyCollection<AuditEntityRef>>(t =>
                t.Contains(new AuditEntityRef("Page", "page-1")) &&
                t.Contains(new AuditEntityRef("PageVersion", "v1")) &&
                t.Contains(new AuditEntityRef("PageVersion", "v2"))),
            50,
            Arg.Any<CancellationToken>());
        // Single-entity path is NOT hit when children are present.
        await _auditing.DidNotReceive().GetByEntityAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEntryAsync_AcceptsAuditTargetingResolvedChild()
    {
        // Audit row targets PageVersion v1, anchored under /timeline/Page/page-1.
        // The security check must let it through because v1 IS a child of page-1.
        var auditId = Guid.NewGuid();
        var entry = new AuditEntry
        {
            Id = auditId,
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "u-1",
            EntityChanges = [new AuditEntityChange { EntityType = "PageVersion", EntityId = "v1", ChangeType = AuditChangeType.Modified }],
        };
        _auditing.GetByIdAsync(auditId, Arg.Any<CancellationToken>()).Returns(entry);

        FakeChildResolver resolver = new("Page", "page-1",
            [new AuditChildScope("PageVersion", ["v1"])]);
        AuditingTimelineSource source = new(_auditing, [], [resolver]);

        TimelineStreamEntry? result = await source
            .GetEntryAsync("Page", "page-1", auditId.ToString(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.SourceId.ShouldBe(auditId.ToString("D"));
        // Body reflects the child mutation, not a (missing) parent change.
        using var payload = JsonDocument.Parse(result.Body);
        payload.RootElement.GetProperty("entityType").GetString().ShouldBe("PageVersion");
        payload.RootElement.GetProperty("entityId").GetString().ShouldBe("v1");
    }

    [Fact]
    public async Task GetEntryAsync_RejectsAuditTargetingChildOfDifferentParent()
    {
        // Audit row targets PageVersion v1, anchored under /timeline/Page/page-2.
        // Resolver returns v1 as child of page-1 only — security check must
        // refuse, preventing cross-aggregate anchoring via forged URLs.
        var auditId = Guid.NewGuid();
        var entry = new AuditEntry
        {
            Id = auditId,
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "u-1",
            EntityChanges = [new AuditEntityChange { EntityType = "PageVersion", EntityId = "v1", ChangeType = AuditChangeType.Modified }],
        };
        _auditing.GetByIdAsync(auditId, Arg.Any<CancellationToken>()).Returns(entry);

        // Resolver only yields children for page-1, not page-2.
        FakeChildResolver resolver = new("Page", "page-2", []);
        AuditingTimelineSource source = new(_auditing, [], [resolver]);

        TimelineStreamEntry? result = await source
            .GetEntryAsync("Page", "page-2", auditId.ToString(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    private static AuditEntry MakeEntry(string type, string id, AuditChangeType changeType) => new()
    {
        Id = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        UserId = "u-1",
        Category = AuditCategory.DataMutation,
        EntityChanges = [new AuditEntityChange { EntityType = type, EntityId = id, ChangeType = changeType }],
    };

    private sealed class FakeChildResolver(
        string expectedParentType,
        string expectedParentId,
        IReadOnlyCollection<AuditChildScope> scopes) : IAuditChildResolver
    {
        public Task<IReadOnlyCollection<AuditChildScope>> ResolveAsync(
            string parentEntityType, string parentEntityId, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(parentEntityType, expectedParentType, StringComparison.Ordinal) ||
                !string.Equals(parentEntityId, expectedParentId, StringComparison.Ordinal))
            {
                return Task.FromResult<IReadOnlyCollection<AuditChildScope>>([]);
            }
            return Task.FromResult(scopes);
        }
    }
}
