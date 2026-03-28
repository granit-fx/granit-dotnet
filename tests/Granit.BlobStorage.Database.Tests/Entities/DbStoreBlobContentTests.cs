using Granit.BlobStorage.Database.Entities;
using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Database.Tests.Entities;

public sealed class DbStoreBlobContentTests
{
    [Fact]
    public void Id_DefaultsToEmpty() =>
        new DbStoreBlobContent().Id.ShouldBe(Guid.Empty);

    [Fact]
    public void TenantId_DefaultsToNull() =>
        new DbStoreBlobContent().TenantId.ShouldBeNull();

    [Fact]
    public void ObjectKey_DefaultsToEmpty() =>
        new DbStoreBlobContent().ObjectKey.ShouldBe(string.Empty);

    [Fact]
    public void Content_DefaultsToEmptyArray() =>
        new DbStoreBlobContent().Content.ShouldBeEmpty();

    [Fact]
    public void CreatedAt_DefaultsToMinValue() =>
        new DbStoreBlobContent().CreatedAt.ShouldBe(default);

    [Fact]
    public void Implements_IMultiTenant() =>
        typeof(DbStoreBlobContent).GetInterfaces().ShouldContain(typeof(IMultiTenant));

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        byte[] content = [0xDE, 0xAD, 0xBE, 0xEF];

        DbStoreBlobContent entity = new()
        {
            Id = id,
            TenantId = tenantId,
            ObjectKey = "tenant/images/2026/03/photo.jpg",
            Content = content,
            CreatedAt = now,
        };

        entity.Id.ShouldBe(id);
        entity.TenantId.ShouldBe(tenantId);
        entity.ObjectKey.ShouldBe("tenant/images/2026/03/photo.jpg");
        entity.Content.ShouldBe(content);
        entity.CreatedAt.ShouldBe(now);
    }
}
