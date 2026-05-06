using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Events;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Tests.Domain;

public sealed class CategoryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void CreateRoot_PopulatesPathAndEmitsEvent()
    {
        var root = Category.CreateRoot(Guid.NewGuid(), TenantId, "products", "electronics");

        root.ParentId.ShouldBeNull();
        root.Path.ShouldBe("/electronics");
        root.Depth.ShouldBe(0);
        root.Scope.ShouldBe("products");
        root.DomainEvents.OfType<CategoryCreatedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Create_UnderParent_ComposesPathAndDepth()
    {
        var parent = Category.CreateRoot(Guid.NewGuid(), TenantId, "products", "electronics");
        var child = Category.Create(Guid.NewGuid(), parent, "laptops");

        child.ParentId.ShouldBe(parent.Id);
        child.Path.ShouldBe("/electronics/laptops");
        child.Depth.ShouldBe(1);
        child.Scope.ShouldBe("products");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has/slash")]
    public void CreateRoot_InvalidName_Throws(string name) =>
        Should.Throw<ArgumentException>(() =>
            Category.CreateRoot(Guid.NewGuid(), TenantId, "products", name));

    [Fact]
    public void Rename_RootCategory_UpdatesPath()
    {
        var root = Category.CreateRoot(Guid.NewGuid(), TenantId, "products", "electronics");
        root.ClearDomainEvents();

        root.Rename("hardware");

        root.Path.ShouldBe("/hardware");
        root.RowVersion.ShouldBe(2u);
        root.DomainEvents.OfType<CategoryRenamedEvent>().ShouldHaveSingleItem();
        root.DomainEvents.OfType<CategoryTreePathChangedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Rename_ChildCategory_UpdatesPathPreservingParent()
    {
        var parent = Category.CreateRoot(Guid.NewGuid(), TenantId, "products", "electronics");
        var child = Category.Create(Guid.NewGuid(), parent, "laptops");
        child.ClearDomainEvents();

        child.Rename("notebooks");

        child.Path.ShouldBe("/electronics/notebooks");
    }

    [Fact]
    public void Rename_SameName_NoOp()
    {
        var root = Category.CreateRoot(Guid.NewGuid(), TenantId, "products", "electronics");
        root.ClearDomainEvents();

        root.Rename("electronics");

        root.RowVersion.ShouldBe(1u);
        root.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void MoveTo_NewParent_RecomputesPathAndDepth()
    {
        var parent1 = Category.CreateRoot(Guid.NewGuid(), TenantId, "products", "electronics");
        var parent2 = Category.CreateRoot(Guid.NewGuid(), TenantId, "products", "hardware");
        var child = Category.Create(Guid.NewGuid(), parent1, "laptops");
        child.ClearDomainEvents();

        child.MoveTo(parent2);

        child.ParentId.ShouldBe(parent2.Id);
        child.Path.ShouldBe("/hardware/laptops");
        child.Depth.ShouldBe(1);
        child.DomainEvents.OfType<CategoryMovedEvent>().ShouldHaveSingleItem();
        child.DomainEvents.OfType<CategoryTreePathChangedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void MoveTo_Root_ConvertsToRoot()
    {
        var parent = Category.CreateRoot(Guid.NewGuid(), TenantId, "products", "electronics");
        var child = Category.Create(Guid.NewGuid(), parent, "laptops");

        child.MoveTo(null);

        child.ParentId.ShouldBeNull();
        child.Path.ShouldBe("/laptops");
        child.Depth.ShouldBe(0);
    }

    [Fact]
    public void MoveTo_DifferentScope_Throws()
    {
        var parent1 = Category.CreateRoot(Guid.NewGuid(), TenantId, "products", "electronics");
        var parent2 = Category.CreateRoot(Guid.NewGuid(), TenantId, "documents", "shared");
        var child = Category.Create(Guid.NewGuid(), parent1, "laptops");

        Should.Throw<InvalidOperationException>(() => child.MoveTo(parent2));
    }

    [Fact]
    public void MoveTo_DifferentTenant_Throws()
    {
        var parent1 = Category.CreateRoot(Guid.NewGuid(), TenantId, "products", "electronics");
        var parent2 = Category.CreateRoot(Guid.NewGuid(), Guid.NewGuid(), "products", "electronics");
        var child = Category.Create(Guid.NewGuid(), parent1, "laptops");

        Should.Throw<InvalidOperationException>(() => child.MoveTo(parent2));
    }

    [Fact]
    public void MoveTo_OwnDescendant_ThrowsCycle()
    {
        var root = Category.CreateRoot(Guid.NewGuid(), TenantId, "products", "electronics");
        var child = Category.Create(Guid.NewGuid(), root, "laptops");

        Should.Throw<InvalidOperationException>(() => root.MoveTo(child));
    }

    [Fact]
    public void MarkDeleted_EmitsDeletedEvent()
    {
        var root = Category.CreateRoot(Guid.NewGuid(), TenantId, "products", "electronics");
        root.ClearDomainEvents();

        root.MarkDeleted();

        root.DomainEvents.OfType<CategoryDeletedEvent>().ShouldHaveSingleItem();
    }
}

public sealed class CategoryAssignmentTests
{
    [Fact]
    public void Create_PopulatesAllProperties()
    {
        var assignment = CategoryAssignment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Granit.Documents.Domain.Document", Guid.NewGuid(),
            Guid.NewGuid(), DateTimeOffset.UtcNow);

        assignment.TargetType.ShouldBe("Granit.Documents.Domain.Document");
    }

    [Fact]
    public void ChangeCategory_UpdatesIdAndAuditFields()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var assignment = CategoryAssignment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "X", Guid.NewGuid(), Guid.NewGuid(), now);

        var newCategoryId = Guid.NewGuid();
        var newUserId = Guid.NewGuid();
        DateTimeOffset newTime = now.AddMinutes(5);
        assignment.ChangeCategory(newCategoryId, newUserId, newTime);

        assignment.CategoryId.ShouldBe(newCategoryId);
        assignment.AssignedByUserId.ShouldBe(newUserId);
        assignment.AssignedAt.ShouldBe(newTime);
    }

    [Fact]
    public void Create_EmptyTargetId_Throws() =>
        Should.Throw<ArgumentException>(() =>
            CategoryAssignment.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "X", Guid.Empty, Guid.NewGuid(), DateTimeOffset.UtcNow));
}
