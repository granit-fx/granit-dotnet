// =============================================================================
// Tests — EfCoreTimelineStore
// =============================================================================
// Verifies CRUD operations, SystemLog immutability guard, and attachment handling
// against a SQLite in-memory database.
// =============================================================================

using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timeline.Domain;
using Granit.Timeline.EntityFrameworkCore.Internal;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Timeline.EntityFrameworkCore.Tests;

public sealed class EfCoreTimelineStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly EfCoreTimelineStore _store;
    private readonly IClock _clock;

    public EfCoreTimelineStoreTests()
    {
        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(DateTimeOffset.UtcNow);

        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.UserId.Returns("test-user");
        userService.UserName.Returns("Test User");

        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);

        _store = new EfCoreTimelineStore(_factory, _clock, userService, guidGenerator, tenant);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task PostEntryAsync_Comment_PersistsEntry()
    {
        TimelineEntry entry = await _store.PostEntryAsync(
            "Patient", "p-1", TimelineEntryType.Comment, "Hello world",
            cancellationToken: TestContext.Current.CancellationToken);

        entry.EntityType.ShouldBe("Patient");
        entry.EntityId.ShouldBe("p-1");
        entry.EntryType.ShouldBe(TimelineEntryType.Comment);
        entry.Body.ShouldBe("Hello world");
        entry.AuthorId.ShouldBe("test-user");
        entry.AuthorName.ShouldBe("Test User");
        entry.IsDeleted.ShouldBeFalse();

        // Verify persistence via a fresh DbContext
        await using TimelineDbContext db = _factory.CreateDbContext();
        TimelineEntry? persisted = await db.TimelineEntries.FindAsync([entry.Id], TestContext.Current.CancellationToken);
        persisted.ShouldNotBeNull();
        persisted!.Body.ShouldBe("Hello world");
    }

    [Fact]
    public async Task PostEntryAsync_WithParentEntryId_SetsParent()
    {
        TimelineEntry parent = await _store.PostEntryAsync(
            "Patient", "p-1", TimelineEntryType.Comment, "Parent",
            cancellationToken: TestContext.Current.CancellationToken);

        TimelineEntry reply = await _store.PostEntryAsync(
            "Patient", "p-1", TimelineEntryType.Comment, "Reply",
            parentEntryId: parent.Id,
            cancellationToken: TestContext.Current.CancellationToken);

        reply.ParentEntryId.ShouldBe(parent.Id);
    }

    [Fact]
    public async Task DeleteEntryAsync_Comment_SoftDeletes()
    {
        TimelineEntry entry = await _store.PostEntryAsync(
            "Patient", "p-1", TimelineEntryType.Comment, "To delete",
            cancellationToken: TestContext.Current.CancellationToken);

        await _store.DeleteEntryAsync(entry.Id, TestContext.Current.CancellationToken);

        // Verify soft-delete via a fresh DbContext (bypass query filter)
        await using TimelineDbContext db = _factory.CreateDbContext();
        TimelineEntry? persisted = await db.TimelineEntries
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == entry.Id, TestContext.Current.CancellationToken);
        persisted.ShouldNotBeNull();
        persisted!.IsDeleted.ShouldBeTrue();
        persisted.DeletedAt.ShouldNotBeNull();
        persisted.DeletedBy.ShouldBe("test-user");
    }

    [Fact]
    public async Task DeleteEntryAsync_SystemLog_ThrowsInvalidOperationException()
    {
        TimelineEntry entry = await _store.PostEntryAsync(
            "Invoice", "inv-42", TimelineEntryType.SystemLog, "{\"change\":\"status\"}",
            cancellationToken: TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _store.DeleteEntryAsync(entry.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteEntryAsync_NonExistentEntry_ThrowsKeyNotFound()
    {
        await Should.ThrowAsync<KeyNotFoundException>(
            () => _store.DeleteEntryAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddAttachmentAsync_PersistsAttachment()
    {
        TimelineEntry entry = await _store.PostEntryAsync(
            "Patient", "p-1", TimelineEntryType.Comment, "See attached",
            cancellationToken: TestContext.Current.CancellationToken);

        var blobId = Guid.NewGuid();
        TimelineAttachment attachment = await _store.AddAttachmentAsync(
            entry.Id, blobId, "report.pdf", "application/pdf", 1024,
            cancellationToken: TestContext.Current.CancellationToken);

        attachment.EntryId.ShouldBe(entry.Id);
        attachment.BlobId.ShouldBe(blobId);
        attachment.FileName.ShouldBe("report.pdf");
        attachment.ContentType.ShouldBe("application/pdf");
        attachment.SizeBytes.ShouldBe(1024);

        // Verify persistence
        await using TimelineDbContext db = _factory.CreateDbContext();
        TimelineAttachment? persisted = await db.TimelineAttachments.FindAsync([attachment.Id], TestContext.Current.CancellationToken);
        persisted.ShouldNotBeNull();
        persisted!.FileName.ShouldBe("report.pdf");
    }

    [Fact]
    public async Task AddAttachmentAsync_NonExistentEntry_ThrowsKeyNotFound()
    {
        await Should.ThrowAsync<KeyNotFoundException>(
            () => _store.AddAttachmentAsync(
                Guid.NewGuid(), Guid.NewGuid(), "file.txt", "text/plain", 100,
                cancellationToken: TestContext.Current.CancellationToken));
    }
}
