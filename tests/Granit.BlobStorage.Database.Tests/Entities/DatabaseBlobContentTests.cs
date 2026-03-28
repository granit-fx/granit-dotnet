using Granit.BlobStorage.Database.Entities;
using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Database.Tests.Entities;

public sealed class DatabaseBlobContentTests
{
    [Fact]
    public void Id_DefaultsToEmpty() =>
        new DatabaseBlobContent().Id.ShouldBe(Guid.Empty);

    [Fact]
    public void TenantId_DefaultsToNull() =>
        new DatabaseBlobContent().TenantId.ShouldBeNull();

    [Fact]
    public void ObjectKey_DefaultsToEmpty() =>
        new DatabaseBlobContent().ObjectKey.ShouldBe(string.Empty);

    [Fact]
    public void Content_DefaultsToEmptyArray() =>
        new DatabaseBlobContent().Content.ShouldBeEmpty();

    [Fact]
    public void CreatedAt_DefaultsToMinValue() =>
        new DatabaseBlobContent().CreatedAt.ShouldBe(default);

    [Fact]
    public void Implements_IMultiTenant() =>
        typeof(DatabaseBlobContent).GetInterfaces().ShouldContain(typeof(IMultiTenant));

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        byte[] content = [1, 2, 3];

        DatabaseBlobContent entity = new()
        {
            Id = id,
            TenantId = tenantId,
            ObjectKey = "tenant/container/2026/01/blob-id",
            Content = content,
            CreatedAt = now,
        };

        entity.Id.ShouldBe(id);
        entity.TenantId.ShouldBe(tenantId);
        entity.ObjectKey.ShouldBe("tenant/container/2026/01/blob-id");
        entity.Content.ShouldBe(content);
        entity.CreatedAt.ShouldBe(now);
    }
}
