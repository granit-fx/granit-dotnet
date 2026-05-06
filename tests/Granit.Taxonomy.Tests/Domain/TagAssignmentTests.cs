using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Registration;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Tests.Domain;

public sealed class TagAssignmentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid TagId = Guid.NewGuid();
    private static readonly Guid TargetId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_PopulatesAllProperties()
    {
        var assignment = TagAssignment.Create(
            Guid.NewGuid(), TenantId, TagId, "Granit.Documents.Domain.Document", TargetId, UserId, Now);

        assignment.TenantId.ShouldBe(TenantId);
        assignment.TagId.ShouldBe(TagId);
        assignment.TargetType.ShouldBe("Granit.Documents.Domain.Document");
        assignment.TargetId.ShouldBe(TargetId);
        assignment.AssignedByUserId.ShouldBe(UserId);
        assignment.AssignedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankTargetType_Throws(string targetType) =>
        Should.Throw<ArgumentException>(() =>
            TagAssignment.Create(Guid.NewGuid(), TenantId, TagId, targetType, TargetId, UserId, Now));

    [Fact]
    public void Create_TargetTypeTooLong_Throws()
    {
        string tooLong = new('x', TagAssignment.MaxTargetTypeLength + 1);
        Should.Throw<ArgumentException>(() =>
            TagAssignment.Create(Guid.NewGuid(), TenantId, TagId, tooLong, TargetId, UserId, Now));
    }

    [Fact]
    public void Create_EmptyTargetId_Throws() =>
        Should.Throw<ArgumentException>(() =>
            TagAssignment.Create(Guid.NewGuid(), TenantId, TagId, "X", Guid.Empty, UserId, Now));
}

public sealed class TaggableTypeRegistryTests
{
    [Fact]
    public void Register_NewType_StoresScope()
    {
        TaggableTypeRegistry registry = new();
        registry.Register("Granit.Documents.Domain.Document", "documents");

        registry.IsRegistered("Granit.Documents.Domain.Document").ShouldBeTrue();
        registry.GetScope("Granit.Documents.Domain.Document").ShouldBe("documents");
    }

    [Fact]
    public void Register_SameTypeSameScope_NoOp()
    {
        TaggableTypeRegistry registry = new();
        registry.Register("X", "documents");
        Should.NotThrow(() => registry.Register("X", "documents"));
    }

    [Fact]
    public void Register_SameTypeDifferentScope_Throws()
    {
        TaggableTypeRegistry registry = new();
        registry.Register("X", "documents");
        Should.Throw<InvalidOperationException>(() => registry.Register("X", "parties"));
    }

    [Fact]
    public void Snapshot_ReturnsAllRegistrations()
    {
        TaggableTypeRegistry registry = new();
        registry.Register("X", "documents");
        registry.Register("Y", "parties");

        IReadOnlyDictionary<string, string> snapshot = registry.Snapshot;
        snapshot.Count.ShouldBe(2);
        snapshot["X"].ShouldBe("documents");
        snapshot["Y"].ShouldBe("parties");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_BlankTargetType_Throws(string targetType) =>
        Should.Throw<ArgumentException>(() =>
            new TaggableTypeRegistry().Register(targetType, "documents"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_BlankScope_Throws(string scope) =>
        Should.Throw<ArgumentException>(() =>
            new TaggableTypeRegistry().Register("X", scope));
}
