using Granit.Documents.Domain;
using Granit.Documents.Events;
using Shouldly;
using Xunit;

namespace Granit.Documents.Tests.Domain;

public sealed class FolderTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static Folder NewRoot() =>
        InvokeInternalCreateTenantRoot(Guid.NewGuid(), TenantId, OwnerId);

    /// <summary>
    /// <c>Folder.CreateTenantRoot</c> is internal; tests reside in
    /// <c>Granit.Documents.Tests</c> which has <c>InternalsVisibleTo</c>, so the
    /// member is reachable directly. This helper documents intent.
    /// </summary>
    private static Folder InvokeInternalCreateTenantRoot(Guid id, Guid? tenantId, Guid ownerId) =>
        Folder.CreateTenantRoot(id, tenantId, ownerId);

    [Fact]
    public void CreateTenantRoot_Defaults_AreCorrect()
    {
        Folder root = NewRoot();

        root.IsTenantRoot.ShouldBeTrue();
        root.ParentFolderId.ShouldBeNull();
        root.Path.ShouldBe("/");
        root.Depth.ShouldBe(0);
        root.Status.ShouldBe(FolderStatus.Active);
        root.TrashedAt.ShouldBeNull();

        FolderCreatedEvent created = root.DomainEvents.OfType<FolderCreatedEvent>().ShouldHaveSingleItem();
        created.IsTenantRoot.ShouldBeTrue();
        created.Path.ShouldBe("/");
    }

    [Fact]
    public void Create_NonRoot_ComputesPathAndDepth()
    {
        Folder root = NewRoot();

        var contracts = Folder.Create(Guid.NewGuid(), root, "Contracts", OwnerId);

        contracts.IsTenantRoot.ShouldBeFalse();
        contracts.ParentFolderId.ShouldBe(root.Id);
        contracts.Path.ShouldBe("/Contracts");
        contracts.Depth.ShouldBe(1);
        contracts.TenantId.ShouldBe(TenantId);

        var year = Folder.Create(Guid.NewGuid(), contracts, "2026", OwnerId);
        year.Path.ShouldBe("/Contracts/2026");
        year.Depth.ShouldBe(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a/b")]
    public void Create_InvalidName_Throws(string name)
    {
        Folder root = NewRoot();

        Should.Throw<ArgumentException>(() => Folder.Create(Guid.NewGuid(), root, name, OwnerId));
    }

    [Fact]
    public void Create_NameTooLong_Throws()
    {
        Folder root = NewRoot();
        string longName = new('a', Folder.MaxNameLength + 1);

        Should.Throw<ArgumentException>(() => Folder.Create(Guid.NewGuid(), root, longName, OwnerId));
    }

    [Fact]
    public void Create_UnderTrashedParent_Throws()
    {
        Folder root = NewRoot();
        var parent = Folder.Create(Guid.NewGuid(), root, "Parent", OwnerId);
        parent.Trash(DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() =>
            Folder.Create(Guid.NewGuid(), parent, "Child", OwnerId));
    }

    [Fact]
    public void Rename_UpdatesNameAndPath_AndEmitsEvents()
    {
        Folder root = NewRoot();
        var folder = Folder.Create(Guid.NewGuid(), root, "Old", OwnerId);

        folder.Rename("New");

        folder.Name.ShouldBe("New");
        folder.Path.ShouldBe("/New");
        folder.DomainEvents.OfType<FolderRenamedEvent>().ShouldHaveSingleItem();
        FolderPathChangedEvent path = folder.DomainEvents.OfType<FolderPathChangedEvent>().ShouldHaveSingleItem();
        path.OldPath.ShouldBe("/Old");
        path.NewPath.ShouldBe("/New");
    }

    [Fact]
    public void Rename_NestedFolder_RecomputesPathFromParent()
    {
        Folder root = NewRoot();
        var a = Folder.Create(Guid.NewGuid(), root, "A", OwnerId);
        var b = Folder.Create(Guid.NewGuid(), a, "B", OwnerId);

        b.Rename("BB");

        b.Path.ShouldBe("/A/BB");
    }

    [Fact]
    public void Rename_TenantRoot_Throws()
    {
        Folder root = NewRoot();

        Should.Throw<InvalidOperationException>(() => root.Rename("Anything"));
    }

    [Fact]
    public void Rename_TrashedFolder_Throws()
    {
        Folder root = NewRoot();
        var folder = Folder.Create(Guid.NewGuid(), root, "F", OwnerId);
        folder.Trash(DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() => folder.Rename("F2"));
    }

    [Fact]
    public void Rename_SameName_IsNoOp()
    {
        Folder root = NewRoot();
        var folder = Folder.Create(Guid.NewGuid(), root, "Same", OwnerId);

        folder.Rename("Same");

        folder.DomainEvents.OfType<FolderRenamedEvent>().ShouldBeEmpty();
        folder.DomainEvents.OfType<FolderPathChangedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void MoveTo_DifferentParent_UpdatesPathDepthAndEmitsEvents()
    {
        Folder root = NewRoot();
        var a = Folder.Create(Guid.NewGuid(), root, "A", OwnerId);
        var b = Folder.Create(Guid.NewGuid(), root, "B", OwnerId);
        var leaf = Folder.Create(Guid.NewGuid(), a, "Leaf", OwnerId);

        leaf.MoveTo(b);

        leaf.ParentFolderId.ShouldBe(b.Id);
        leaf.Path.ShouldBe("/B/Leaf");
        leaf.Depth.ShouldBe(2);
        leaf.DomainEvents.OfType<FolderMovedEvent>().ShouldHaveSingleItem();
        leaf.DomainEvents.OfType<FolderPathChangedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void MoveTo_TenantRoot_Throws()
    {
        Folder root = NewRoot();
        var somewhere = Folder.Create(Guid.NewGuid(), root, "X", OwnerId);

        Should.Throw<InvalidOperationException>(() => root.MoveTo(somewhere));
    }

    [Fact]
    public void MoveTo_Self_Throws()
    {
        Folder root = NewRoot();
        var a = Folder.Create(Guid.NewGuid(), root, "A", OwnerId);

        Should.Throw<InvalidOperationException>(() => a.MoveTo(a));
    }

    [Fact]
    public void MoveTo_DescendantOfSelf_Throws()
    {
        Folder root = NewRoot();
        var a = Folder.Create(Guid.NewGuid(), root, "A", OwnerId);
        var b = Folder.Create(Guid.NewGuid(), a, "B", OwnerId);

        Should.Throw<InvalidOperationException>(() => a.MoveTo(b));
    }

    [Fact]
    public void MoveTo_DifferentTenant_Throws()
    {
        Folder root = NewRoot();
        var a = Folder.Create(Guid.NewGuid(), root, "A", OwnerId);

        var otherRoot = Folder.CreateTenantRoot(Guid.NewGuid(), Guid.NewGuid(), OwnerId);
        var otherFolder = Folder.Create(Guid.NewGuid(), otherRoot, "Other", OwnerId);

        Should.Throw<InvalidOperationException>(() => a.MoveTo(otherFolder));
    }

    [Fact]
    public void MoveTo_TrashedTarget_Throws()
    {
        Folder root = NewRoot();
        var a = Folder.Create(Guid.NewGuid(), root, "A", OwnerId);
        var b = Folder.Create(Guid.NewGuid(), root, "B", OwnerId);
        b.Trash(DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() => a.MoveTo(b));
    }

    [Fact]
    public void MoveTo_SameParent_IsNoOp()
    {
        Folder root = NewRoot();
        var a = Folder.Create(Guid.NewGuid(), root, "A", OwnerId);
        var leaf = Folder.Create(Guid.NewGuid(), a, "Leaf", OwnerId);

        leaf.MoveTo(a);

        leaf.DomainEvents.OfType<FolderMovedEvent>().ShouldBeEmpty();
        leaf.DomainEvents.OfType<FolderPathChangedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Trash_SetsStatusAndTrashedAt_AndEmitsEvent()
    {
        Folder root = NewRoot();
        var folder = Folder.Create(Guid.NewGuid(), root, "F", OwnerId);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        folder.Trash(now);

        folder.Status.ShouldBe(FolderStatus.Trashed);
        folder.TrashedAt.ShouldBe(now);
        folder.DomainEvents.OfType<FolderTrashedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Trash_TenantRoot_Throws()
    {
        Folder root = NewRoot();

        Should.Throw<InvalidOperationException>(() => root.Trash(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Trash_AlreadyTrashed_Throws()
    {
        Folder root = NewRoot();
        var folder = Folder.Create(Guid.NewGuid(), root, "F", OwnerId);
        folder.Trash(DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() => folder.Trash(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Restore_FromTrash_BecomesActive()
    {
        Folder root = NewRoot();
        var folder = Folder.Create(Guid.NewGuid(), root, "F", OwnerId);
        folder.Trash(DateTimeOffset.UtcNow);

        folder.Restore();

        folder.Status.ShouldBe(FolderStatus.Active);
        folder.TrashedAt.ShouldBeNull();
        folder.DomainEvents.OfType<FolderRestoredEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Restore_TenantRoot_Throws()
    {
        Folder root = NewRoot();

        Should.Throw<InvalidOperationException>(() => root.Restore());
    }

    [Fact]
    public void Restore_ActiveFolder_Throws()
    {
        Folder root = NewRoot();
        var folder = Folder.Create(Guid.NewGuid(), root, "F", OwnerId);

        Should.Throw<InvalidOperationException>(() => folder.Restore());
    }
}
