using Granit.Documents.Domain;
using Granit.Documents.Events;
using Shouldly;
using Xunit;

namespace Granit.Documents.Tests.Domain;

public sealed class DocumentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static Folder NewRoot() =>
        Folder.CreateTenantRoot(Guid.NewGuid(), TenantId, OwnerId);

    private static Folder NewFolder(Folder root, string name) =>
        Folder.Create(Guid.NewGuid(), root, name, OwnerId);

    [Fact]
    public void Create_DefaultProperties_AreCorrect()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "Contracts");

        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "Q1-2026.pdf", "Quarter 1 close");

        doc.TenantId.ShouldBe(TenantId);
        doc.FolderId.ShouldBe(folder.Id);
        doc.OwnerUserId.ShouldBe(OwnerId);
        doc.Name.ShouldBe("Q1-2026.pdf");
        doc.Description.ShouldBe("Quarter 1 close");
        doc.CurrentVersionId.ShouldBeNull();
        doc.Status.ShouldBe(DocumentStatus.Active);
        doc.TrashedAt.ShouldBeNull();
        doc.RowVersion.ShouldBe(1u);
        doc.DomainEvents.OfType<DocumentCreatedEvent>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_InvalidName_Throws(string name)
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");

        Should.Throw<ArgumentException>(() => Document.Create(Guid.NewGuid(), folder, OwnerId, name));
    }

    [Fact]
    public void Create_NameTooLong_Throws()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");

        Should.Throw<ArgumentException>(
            () => Document.Create(Guid.NewGuid(), folder, OwnerId, new string('a', Document.MaxNameLength + 1)));
    }

    [Fact]
    public void Create_DescriptionTooLong_Throws()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");
        string desc = new('d', Document.MaxDescriptionLength + 1);

        Should.Throw<ArgumentException>(
            () => Document.Create(Guid.NewGuid(), folder, OwnerId, "name.pdf", desc));
    }

    [Fact]
    public void Create_TrashedFolder_Throws()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");
        folder.Trash(DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(
            () => Document.Create(Guid.NewGuid(), folder, OwnerId, "x.pdf"));
    }

    [Fact]
    public void Rename_UpdatesNameAndIncrementsRowVersion()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");
        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "Old.pdf");
        uint before = doc.RowVersion;

        doc.Rename("New.pdf");

        doc.Name.ShouldBe("New.pdf");
        doc.RowVersion.ShouldBeGreaterThan(before);
        doc.DomainEvents.OfType<DocumentRenamedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Rename_SameName_IsNoOp()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");
        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "Same.pdf");
        uint before = doc.RowVersion;

        doc.Rename("Same.pdf");

        doc.RowVersion.ShouldBe(before);
        doc.DomainEvents.OfType<DocumentRenamedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Rename_TrashedDocument_Throws()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");
        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "F.pdf");
        doc.Trash(DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() => doc.Rename("X.pdf"));
    }

    [Fact]
    public void UpdateDescription_UpdatesAndIncrementsRowVersion()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");
        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "F.pdf", "old");
        uint before = doc.RowVersion;

        doc.UpdateDescription("new");

        doc.Description.ShouldBe("new");
        doc.RowVersion.ShouldBeGreaterThan(before);
    }

    [Fact]
    public void UpdateDescription_SameValue_IsNoOp()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");
        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "F.pdf", "same");
        uint before = doc.RowVersion;

        doc.UpdateDescription("same");

        doc.RowVersion.ShouldBe(before);
    }

    [Fact]
    public void MoveTo_DifferentFolder_UpdatesFolderIdAndEmitsEvent()
    {
        Folder root = NewRoot();
        Folder a = NewFolder(root, "A");
        Folder b = NewFolder(root, "B");
        var doc = Document.Create(Guid.NewGuid(), a, OwnerId, "F.pdf");

        doc.MoveTo(b);

        doc.FolderId.ShouldBe(b.Id);
        DocumentMovedEvent moved = doc.DomainEvents.OfType<DocumentMovedEvent>().ShouldHaveSingleItem();
        moved.OldFolderId.ShouldBe(a.Id);
        moved.NewFolderId.ShouldBe(b.Id);
    }

    [Fact]
    public void MoveTo_SameFolder_IsNoOp()
    {
        Folder root = NewRoot();
        Folder a = NewFolder(root, "A");
        var doc = Document.Create(Guid.NewGuid(), a, OwnerId, "F.pdf");
        uint before = doc.RowVersion;

        doc.MoveTo(a);

        doc.RowVersion.ShouldBe(before);
        doc.DomainEvents.OfType<DocumentMovedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void MoveTo_DifferentTenant_Throws()
    {
        Folder rootA = NewRoot();
        Folder folderA = NewFolder(rootA, "A");
        var doc = Document.Create(Guid.NewGuid(), folderA, OwnerId, "F.pdf");

        var rootB = Folder.CreateTenantRoot(Guid.NewGuid(), Guid.NewGuid(), OwnerId);
        Folder folderB = NewFolder(rootB, "B");

        Should.Throw<InvalidOperationException>(() => doc.MoveTo(folderB));
    }

    [Fact]
    public void MoveTo_TrashedTarget_Throws()
    {
        Folder root = NewRoot();
        Folder a = NewFolder(root, "A");
        Folder b = NewFolder(root, "B");
        b.Trash(DateTimeOffset.UtcNow);
        var doc = Document.Create(Guid.NewGuid(), a, OwnerId, "F.pdf");

        Should.Throw<InvalidOperationException>(() => doc.MoveTo(b));
    }

    [Fact]
    public void SetCurrentVersion_UpdatesAndEmitsEvent()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");
        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "F.pdf");
        var versionId = Guid.NewGuid();

        doc.SetCurrentVersion(versionId);

        doc.CurrentVersionId.ShouldBe(versionId);
        DocumentCurrentVersionChangedEvent ev = doc.DomainEvents
            .OfType<DocumentCurrentVersionChangedEvent>().ShouldHaveSingleItem();
        ev.OldCurrentVersionId.ShouldBeNull();
        ev.NewCurrentVersionId.ShouldBe(versionId);
    }

    [Fact]
    public void SetCurrentVersion_EmptyGuid_Throws()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");
        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "F.pdf");

        Should.Throw<ArgumentException>(() => doc.SetCurrentVersion(Guid.Empty));
    }

    [Fact]
    public void SetCurrentVersion_SameValue_IsNoOp()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");
        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "F.pdf");
        var versionId = Guid.NewGuid();
        doc.SetCurrentVersion(versionId);
        uint before = doc.RowVersion;

        doc.SetCurrentVersion(versionId);

        doc.RowVersion.ShouldBe(before);
    }

    [Fact]
    public void Trash_SetsStatusAndTrashedAt_AndEmitsEvent()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");
        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "F.pdf");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        doc.Trash(now);

        doc.Status.ShouldBe(DocumentStatus.Trashed);
        doc.TrashedAt.ShouldBe(now);
        doc.DomainEvents.OfType<DocumentTrashedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Trash_AlreadyTrashed_Throws()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");
        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "F.pdf");
        doc.Trash(DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() => doc.Trash(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Restore_FromTrash_BecomesActive()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");
        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "F.pdf");
        doc.Trash(DateTimeOffset.UtcNow);

        doc.Restore();

        doc.Status.ShouldBe(DocumentStatus.Active);
        doc.TrashedAt.ShouldBeNull();
        doc.DomainEvents.OfType<DocumentRestoredEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Restore_ActiveDocument_Throws()
    {
        Folder root = NewRoot();
        Folder folder = NewFolder(root, "F");
        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "F.pdf");

        Should.Throw<InvalidOperationException>(() => doc.Restore());
    }
}
