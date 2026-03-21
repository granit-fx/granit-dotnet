using Shouldly;
using Xunit;

namespace Granit.Persistence.Migrations.Tests;

public sealed class MigrationBatchContextTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();
        MigrationBatchContext context = new("cursor-123", 500, tenantId);

        context.Cursor.ShouldBe("cursor-123");
        context.Size.ShouldBe(500);
        context.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Constructor_NullCursor_IsAllowed()
    {
        MigrationBatchContext context = new(null, 100, Guid.Empty);

        context.Cursor.ShouldBeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var tenantId = Guid.NewGuid();
        MigrationBatchContext context1 = new("cursor", 100, tenantId);
        MigrationBatchContext context2 = new("cursor", 100, tenantId);

        context1.ShouldBe(context2);
    }

    [Fact]
    public void Equality_DifferentCursor_AreNotEqual()
    {
        MigrationBatchContext context1 = new("cursor-a", 100, Guid.Empty);
        MigrationBatchContext context2 = new("cursor-b", 100, Guid.Empty);

        context1.ShouldNotBe(context2);
    }

    [Fact]
    public void Equality_DifferentSize_AreNotEqual()
    {
        MigrationBatchContext context1 = new("cursor", 100, Guid.Empty);
        MigrationBatchContext context2 = new("cursor", 200, Guid.Empty);

        context1.ShouldNotBe(context2);
    }

    [Fact]
    public void ToString_ContainsPropertyValues()
    {
        MigrationBatchContext context = new("cursor-abc", 500, Guid.Empty);

        string result = context.ToString();

        result.ShouldContain("cursor-abc");
        result.ShouldContain("500");
    }
}
