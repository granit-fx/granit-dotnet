using Granit.Documents.Domain;
using Granit.Documents.Events;
using Shouldly;
using Xunit;

namespace Granit.Documents.Tests.Domain;

public sealed class DocumentShareTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid GranteeId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShareToFolder_Defaults_AreCorrect()
    {
        var folderId = Guid.NewGuid();
        var id = Guid.NewGuid();

        var share = DocumentShare.ShareToFolder(
            id, TenantId, folderId, ShareGranteeType.User, GranteeId,
            SharePermissionLevel.Read, isDefault: true, CreatedBy, Now);

        share.Id.ShouldBe(id);
        share.TenantId.ShouldBe(TenantId);
        share.TargetType.ShouldBe(ShareTargetType.Folder);
        share.FolderId.ShouldBe(folderId);
        share.DocumentId.ShouldBeNull();
        share.GranteeType.ShouldBe(ShareGranteeType.User);
        share.GranteeId.ShouldBe(GranteeId);
        share.Permission.ShouldBe(SharePermissionLevel.Read);
        share.IsDefault.ShouldBeTrue();
        share.CreatedAt.ShouldBe(Now);
        share.CreatedByUserId.ShouldBe(CreatedBy);
        share.ExpiresAt.ShouldBeNull();

        DocumentShareGrantedEvent granted = share.DomainEvents
            .OfType<DocumentShareGrantedEvent>()
            .ShouldHaveSingleItem();
        granted.TargetType.ShouldBe(ShareTargetType.Folder);
        granted.FolderId.ShouldBe(folderId);
        granted.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void ShareToDocument_AlwaysClearsIsDefault()
    {
        var documentId = Guid.NewGuid();

        var share = DocumentShare.ShareToDocument(
            Guid.NewGuid(), TenantId, documentId, ShareGranteeType.Role, GranteeId,
            SharePermissionLevel.Edit, CreatedBy, Now);

        share.TargetType.ShouldBe(ShareTargetType.Document);
        share.DocumentId.ShouldBe(documentId);
        share.FolderId.ShouldBeNull();
        share.IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void ShareToFolder_EmptyFolderId_Throws() =>
        Should.Throw<ArgumentException>(() => DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, Guid.Empty, ShareGranteeType.User, GranteeId,
            SharePermissionLevel.Read, isDefault: true, CreatedBy, Now));

    [Fact]
    public void ShareToDocument_EmptyDocumentId_Throws() =>
        Should.Throw<ArgumentException>(() => DocumentShare.ShareToDocument(
            Guid.NewGuid(), TenantId, Guid.Empty, ShareGranteeType.User, GranteeId,
            SharePermissionLevel.Read, CreatedBy, Now));

    [Fact]
    public void EmptyGranteeId_Throws() =>
        Should.Throw<ArgumentException>(() => DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, Guid.NewGuid(), ShareGranteeType.User, Guid.Empty,
            SharePermissionLevel.Read, isDefault: true, CreatedBy, Now));

    [Fact]
    public void UnknownGranteeType_Throws() =>
        Should.Throw<ArgumentException>(() => DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, Guid.NewGuid(), (ShareGranteeType)42, GranteeId,
            SharePermissionLevel.Read, isDefault: true, CreatedBy, Now));

    [Fact]
    public void UnknownPermission_Throws() =>
        Should.Throw<ArgumentException>(() => DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, Guid.NewGuid(), ShareGranteeType.User, GranteeId,
            (SharePermissionLevel)42, isDefault: true, CreatedBy, Now));

    [Fact]
    public void ExpiresAt_AtOrBeforeCreatedAt_Throws()
    {
        Should.Throw<ArgumentException>(() => DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, Guid.NewGuid(), ShareGranteeType.User, GranteeId,
            SharePermissionLevel.Read, isDefault: true, CreatedBy, Now, expiresAt: Now));
        Should.Throw<ArgumentException>(() => DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, Guid.NewGuid(), ShareGranteeType.User, GranteeId,
            SharePermissionLevel.Read, isDefault: true, CreatedBy, Now, expiresAt: Now.AddDays(-1)));
    }

    [Fact]
    public void IsExpired_BeforeAndAfterExpiresAt()
    {
        DateTimeOffset expiresAt = Now.AddHours(1);
        var share = DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, Guid.NewGuid(), ShareGranteeType.User, GranteeId,
            SharePermissionLevel.Read, isDefault: true, CreatedBy, Now, expiresAt: expiresAt);

        share.IsExpired(Now).ShouldBeFalse();
        share.IsExpired(expiresAt.AddTicks(-1)).ShouldBeFalse();
        share.IsExpired(expiresAt).ShouldBeTrue();
        share.IsExpired(expiresAt.AddSeconds(1)).ShouldBeTrue();
    }

    [Fact]
    public void IsExpired_NeverExpiringShare_AlwaysFalse()
    {
        var share = DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, Guid.NewGuid(), ShareGranteeType.User, GranteeId,
            SharePermissionLevel.Read, isDefault: true, CreatedBy, Now);

        share.IsExpired(Now.AddYears(100)).ShouldBeFalse();
    }

    [Fact]
    public void Revoke_EmitsRevokedEvent()
    {
        var folderId = Guid.NewGuid();
        var share = DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, folderId, ShareGranteeType.Group, GranteeId,
            SharePermissionLevel.Manage, isDefault: false, CreatedBy, Now);
        // Drop the granted event so we can assert the revoked one cleanly.
        share.ClearDomainEvents();

        share.Revoke();

        DocumentShareRevokedEvent revoked = share.DomainEvents
            .OfType<DocumentShareRevokedEvent>()
            .ShouldHaveSingleItem();
        revoked.TargetType.ShouldBe(ShareTargetType.Folder);
        revoked.FolderId.ShouldBe(folderId);
        revoked.GranteeType.ShouldBe(ShareGranteeType.Group);
    }
}
