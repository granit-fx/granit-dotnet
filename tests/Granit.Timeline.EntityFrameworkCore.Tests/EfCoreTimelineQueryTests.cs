// =============================================================================
// Tests — EfCoreTimelineQuery
// =============================================================================
// Verifies paginated stream queries, ordering, soft-delete filtering, and
// attachment inclusion against a SQLite in-memory database.
// =============================================================================

using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Querying;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.EntityFrameworkCore.Internal;
using Granit.Timing;
using Granit.Users;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Timeline.EntityFrameworkCore.Tests;

public sealed class EfCoreTimelineQueryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly EfCoreTimelineStore _store;
    private readonly EfCoreTimelineQuery _query;
    private readonly IClock _clock;
    private int _timeOffset;

    public EfCoreTimelineQueryTests()
    {
        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(_ => DateTimeOffset.UtcNow.AddMinutes(_timeOffset++));

        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.UserId.Returns("test-user");
        userService.UserName.Returns("Test User");

        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);

        _store = new EfCoreTimelineStore(_factory, _clock, userService, guidGenerator, tenant);
        _query = new EfCoreTimelineQuery(_factory);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetStreamAsync_EmptyStream_ReturnsEmptyPage()
    {
        PagedResult<TimelineStreamEntry> page = await _query.GetStreamAsync(
            "Patient", "p-1", cancellationToken: TestContext.Current.CancellationToken);

        page.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetStreamAsync_ReturnsEntriesOrderedByDateDescending()
    {
        await _store.PostEntryAsync("Patient", "p-1", TimelineEntryType.Comment, "First", cancellationToken: TestContext.Current.CancellationToken);
        await _store.PostEntryAsync("Patient", "p-1", TimelineEntryType.Comment, "Second", cancellationToken: TestContext.Current.CancellationToken);
        await _store.PostEntryAsync("Patient", "p-1", TimelineEntryType.Comment, "Third", cancellationToken: TestContext.Current.CancellationToken);

        PagedResult<TimelineStreamEntry> page = await _query.GetStreamAsync(
            "Patient", "p-1", cancellationToken: TestContext.Current.CancellationToken);

        page.TotalCount.ShouldBe(3);
        page.Items[0].Body.ShouldBe("Third");
        page.Items[1].Body.ShouldBe("Second");
        page.Items[2].Body.ShouldBe("First");
    }

    [Fact]
    public async Task GetStreamAsync_Pagination_PageAndPageSize()
    {
        for (int i = 0; i < 10; i++)
        {
            await _store.PostEntryAsync("Patient", "p-1", TimelineEntryType.Comment, $"Entry {i}", cancellationToken: TestContext.Current.CancellationToken);
        }

        PagedResult<TimelineStreamEntry> page = await _query.GetStreamAsync(
            "Patient", "p-1", page: 2, pageSize: 2, cancellationToken: TestContext.Current.CancellationToken);

        page.TotalCount.ShouldBe(10);
        page.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetStreamAsync_ExcludesSoftDeletedEntries()
    {
        TimelineEntry entry = await _store.PostEntryAsync(
            "Patient", "p-1", TimelineEntryType.Comment, "To delete", cancellationToken: TestContext.Current.CancellationToken);
        await _store.PostEntryAsync("Patient", "p-1", TimelineEntryType.Comment, "Visible", cancellationToken: TestContext.Current.CancellationToken);
        await _store.DeleteEntryAsync(entry.Id, TestContext.Current.CancellationToken);

        PagedResult<TimelineStreamEntry> page = await _query.GetStreamAsync(
            "Patient", "p-1", cancellationToken: TestContext.Current.CancellationToken);

        page.TotalCount.ShouldBe(1);
        page.Items[0].Body.ShouldBe("Visible");
    }

    [Fact]
    public async Task GetStreamAsync_FiltersByEntityTypeAndId()
    {
        await _store.PostEntryAsync("Patient", "p-1", TimelineEntryType.Comment, "Patient entry", cancellationToken: TestContext.Current.CancellationToken);
        await _store.PostEntryAsync("Invoice", "inv-1", TimelineEntryType.Comment, "Invoice entry", cancellationToken: TestContext.Current.CancellationToken);

        PagedResult<TimelineStreamEntry> page = await _query.GetStreamAsync(
            "Patient", "p-1", cancellationToken: TestContext.Current.CancellationToken);

        page.TotalCount.ShouldBe(1);
        page.Items[0].Body.ShouldBe("Patient entry");
    }

    [Fact]
    public async Task GetStreamAsync_IncludesAttachments()
    {
        TimelineEntry entry = await _store.PostEntryAsync(
            "Patient", "p-1", TimelineEntryType.Comment, "With attachment", cancellationToken: TestContext.Current.CancellationToken);
        var blobId = Guid.NewGuid();
        await _store.AddAttachmentAsync(entry.Id, blobId, "report.pdf", "application/pdf", 2048, cancellationToken: TestContext.Current.CancellationToken);

        PagedResult<TimelineStreamEntry> page = await _query.GetStreamAsync(
            "Patient", "p-1", cancellationToken: TestContext.Current.CancellationToken);

        page.Items[0].Attachments.Count.ShouldBe(1);
        page.Items[0].Attachments[0].FileName.ShouldBe("report.pdf");
        page.Items[0].Attachments[0].BlobId.ShouldBe(blobId);
    }

    [Fact]
    public async Task GetStreamAsync_MapsEntryTypes()
    {
        await _store.PostEntryAsync("Patient", "p-1", TimelineEntryType.Comment, "Comment", cancellationToken: TestContext.Current.CancellationToken);
        await _store.PostEntryAsync("Patient", "p-1", TimelineEntryType.SystemLog, "Log", cancellationToken: TestContext.Current.CancellationToken);
        await _store.PostEntryAsync("Patient", "p-1", TimelineEntryType.InternalNote, "Note", cancellationToken: TestContext.Current.CancellationToken);

        PagedResult<TimelineStreamEntry> page = await _query.GetStreamAsync(
            "Patient", "p-1", cancellationToken: TestContext.Current.CancellationToken);

        page.Items.ShouldContain(e => e.EntryType == TimelineStreamEntryType.Comment);
        page.Items.ShouldContain(e => e.EntryType == TimelineStreamEntryType.SystemLog);
        page.Items.ShouldContain(e => e.EntryType == TimelineStreamEntryType.InternalNote);
    }
}
