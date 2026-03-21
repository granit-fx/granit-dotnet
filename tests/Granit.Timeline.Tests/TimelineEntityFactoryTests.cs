using Granit.Core.MultiTenancy;
using Granit.Guids;
using Granit.Security;
using Granit.Timeline.Domain;
using Granit.Timeline.Internal;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineEntityFactoryTests
{
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    public TimelineEntityFactoryTests()
    {
        _clock.Now.Returns(DateTimeOffset.UtcNow);
        _currentUser.UserId.Returns("user-1");
        _currentUser.UserName.Returns("Alice");
        _guidGenerator.Create().Returns(Guid.NewGuid());
        _currentTenant.IsAvailable.Returns(false);
    }

    private AuditContext BuildContext() => new(_guidGenerator, _clock, _currentUser, _currentTenant);

    [Fact]
    public void CreateEntry_SetsAllFields()
    {
        AuditContext context = BuildContext();

        TimelineEntry entry = TimelineEntityFactory.CreateEntry(
            "Patient", "p-1", TimelineEntryType.Comment, "Hello", null, context);

        entry.EntityType.ShouldBe("Patient");
        entry.EntityId.ShouldBe("p-1");
        entry.EntryType.ShouldBe(TimelineEntryType.Comment);
        entry.Body.ShouldBe("Hello");
        entry.AuthorId.ShouldBe("user-1");
        entry.AuthorName.ShouldBe("Alice");
        entry.ParentEntryId.ShouldBeNull();
        entry.TenantId.ShouldBeNull();
    }

    [Fact]
    public void CreateEntry_WithParentEntryId_SetsParent()
    {
        var parentId = Guid.NewGuid();
        AuditContext context = BuildContext();

        TimelineEntry entry = TimelineEntityFactory.CreateEntry(
            "Patient", "p-1", TimelineEntryType.Comment, "Reply", parentId, context);

        entry.ParentEntryId.ShouldBe(parentId);
    }

    [Fact]
    public void CreateEntry_WithTenant_SetsTenantId()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);
        AuditContext context = BuildContext();

        TimelineEntry entry = TimelineEntityFactory.CreateEntry(
            "Patient", "p-1", TimelineEntryType.Comment, "Hello", null, context);

        entry.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void CreateEntry_WithNullUserId_DefaultsToEmptyString()
    {
        _currentUser.UserId.Returns((string?)null);
        _currentUser.UserName.Returns((string?)null);
        AuditContext context = BuildContext();

        TimelineEntry entry = TimelineEntityFactory.CreateEntry(
            "Patient", "p-1", TimelineEntryType.Comment, "Hello", null, context);

        entry.AuthorId.ShouldBe(string.Empty);
        entry.AuthorName.ShouldBe(string.Empty);
    }

    [Fact]
    public void CreateAttachment_SetsAllFields()
    {
        var entryId = Guid.NewGuid();
        var blobId = Guid.NewGuid();
        AuditContext context = BuildContext();

        TimelineAttachment attachment = TimelineEntityFactory.CreateAttachment(
            entryId, blobId, "report.pdf", "application/pdf", 2048, context);

        attachment.EntryId.ShouldBe(entryId);
        attachment.BlobId.ShouldBe(blobId);
        attachment.FileName.ShouldBe("report.pdf");
        attachment.ContentType.ShouldBe("application/pdf");
        attachment.SizeBytes.ShouldBe(2048);
        attachment.CreatedBy.ShouldBe("user-1");
        attachment.TenantId.ShouldBeNull();
    }

    [Fact]
    public void CreateAttachment_WithTenant_SetsTenantId()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);
        AuditContext context = BuildContext();

        TimelineAttachment attachment = TimelineEntityFactory.CreateAttachment(
            Guid.NewGuid(), Guid.NewGuid(), "file.txt", "text/plain", 100, context);

        attachment.TenantId.ShouldBe(tenantId);
    }
}
