using Granit.BlobStorage.DbStore.Entities;
using Granit.Core.Domain;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.DbStore.Tests;

public sealed class DbStoreBlobContentTests
{
    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        byte[] content = [0x25, 0x50, 0x44, 0x46];
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        DbStoreBlobContent entity = new()
        {
            Id = id,
            TenantId = tenantId,
            ObjectKey = "tenant/container/2026/03/blob-id",
            Content = content,
            CreatedAt = createdAt,
        };

        entity.Id.ShouldBe(id);
        entity.TenantId.ShouldBe(tenantId);
        entity.ObjectKey.ShouldBe("tenant/container/2026/03/blob-id");
        entity.Content.ShouldBe(content);
        entity.CreatedAt.ShouldBe(createdAt);
    }

    [Fact]
    public void ImplementsIMultiTenant()
    {
        DbStoreBlobContent entity = new();

        entity.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void DefaultContent_IsEmpty()
    {
        DbStoreBlobContent entity = new();

        entity.Content.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultObjectKey_IsEmpty()
    {
        DbStoreBlobContent entity = new();

        entity.ObjectKey.ShouldBeEmpty();
    }

    [Fact]
    public void TenantId_CanBeNull()
    {
        DbStoreBlobContent entity = new()
        {
            TenantId = null,
        };

        entity.TenantId.ShouldBeNull();
    }
}
