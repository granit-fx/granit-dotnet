using Granit.BlobStorage.Database.Entities;
using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Database.Tests.Entities;

public sealed class DatabaseBlobContentTests
{
    [Fact]
    public void Create_AssignsIdAndObjectKeyAndContent()
    {
        var id = Guid.NewGuid();
        byte[] content = [1, 2, 3];

        var entity = DatabaseBlobContent.Create(id, "tenant/container/2026/01/blob-id", content);

        entity.Id.ShouldBe(id);
        entity.ObjectKey.ShouldBe("tenant/container/2026/01/blob-id");
        entity.Content.ShouldBe(content);
    }

    [Fact]
    public void Create_DefaultsTenantIdToNull()
    {
        var entity = DatabaseBlobContent.Create(Guid.NewGuid(), "key", []);

        entity.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Create_DefaultsCreatedAtToMinValue()
    {
        var entity = DatabaseBlobContent.Create(Guid.NewGuid(), "key", []);

        // CreatedAt is populated by the AuditedEntityInterceptor at SaveChanges time.
        entity.CreatedAt.ShouldBe(default);
    }

    [Fact]
    public void Implements_IMultiTenant() =>
        typeof(DatabaseBlobContent).GetInterfaces().ShouldContain(typeof(IMultiTenant));

    [Fact]
    public void IMultiTenantTenantId_SetterIsAccessible()
    {
        var entity = DatabaseBlobContent.Create(Guid.NewGuid(), "key", []);
        var tenantId = Guid.NewGuid();

        ((IMultiTenant)entity).TenantId = tenantId;

        entity.TenantId.ShouldBe(tenantId);
    }
}
