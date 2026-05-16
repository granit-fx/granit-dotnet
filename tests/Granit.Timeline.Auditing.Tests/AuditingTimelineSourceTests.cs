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
using Granit.Timeline;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Auditing.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Auditing.Tests;

public sealed class AuditingTimelineSourceTests
{
    private readonly IAuditingReader _auditing = Substitute.For<IAuditingReader>();
    private readonly AuditingTimelineSource _source;

    public AuditingTimelineSourceTests() => _source = new AuditingTimelineSource(_auditing);

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
}
