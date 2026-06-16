using Granit.Timeline.Abstractions;
using Granit.Timeline.Endpoints.Dtos;
using Granit.Timeline.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Endpoints.Tests;

public sealed class TimelineResponseMapperFederationTests
{
    [Fact]
    public void ToResponse_maps_native_entry_with_native_source_key_and_no_external_provenance()
    {
        var entry = new TimelineStreamEntry
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            EntryType = TimelineStreamEntryType.Comment,
            AuthorId = "user-1",
            AuthorName = "Alice",
            Body = "Hello",
            // Origin / SourceKey default to Native / "native"; SourceId / EditedAt default to null.
        };

        TimelineStreamEntryResponse response = TimelineResponseMapper.ToResponse(entry);

        response.Origin.ShouldBe(TimelineEntryOrigin.Native);
        response.SourceKey.ShouldBe(TimelineSourceKeys.Native);
        response.SourceId.ShouldBeNull();
        response.EditedAt.ShouldBeNull();
    }

    [Fact]
    public void ToResponse_maps_external_entry_provenance_verbatim()
    {
        var entry = new TimelineStreamEntry
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            EntryType = TimelineStreamEntryType.SystemLog,
            Body = "Audit event",
            Origin = TimelineEntryOrigin.External,
            SourceKey = "auditing",
            SourceId = "audit-42",
        };

        TimelineStreamEntryResponse response = TimelineResponseMapper.ToResponse(entry);

        response.Origin.ShouldBe(TimelineEntryOrigin.External);
        response.SourceKey.ShouldBe("auditing");
        response.SourceId.ShouldBe("audit-42");
        response.EditedAt.ShouldBeNull();
    }

    [Fact]
    public void ToResponse_maps_edited_at_timestamp()
    {
        DateTimeOffset editedAt = DateTimeOffset.UtcNow;
        var entry = new TimelineStreamEntry
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            EntryType = TimelineStreamEntryType.Comment,
            Body = "Edited body",
            EditedAt = editedAt,
        };

        TimelineStreamEntryResponse response = TimelineResponseMapper.ToResponse(entry);

        response.EditedAt.ShouldBe(editedAt);
    }
}
