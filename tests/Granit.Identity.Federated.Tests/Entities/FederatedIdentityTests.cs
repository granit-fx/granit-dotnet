using Granit.Domain;
using Granit.Identity.Federated.Domain;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Tests.Entities;

public sealed class FederatedIdentityTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaults()
    {
        FederatedIdentity entry = new();

        entry.ExternalUserId.ShouldBe(string.Empty);
        entry.Username.ShouldBeNull();
        entry.Email.ShouldBeNull();
        entry.FirstName.ShouldBeNull();
        entry.LastName.ShouldBeNull();
        entry.Enabled.ShouldBeTrue();
        entry.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Properties_AreSettable()
    {
        var tenantId = Guid.NewGuid();
        DateTimeOffset syncedAt = DateTimeOffset.UtcNow;

        FederatedIdentity entry = new()
        {
            ExternalUserId = "ext-123",
            Username = "alice",
            Email = "alice@test.com",
            FirstName = "Alice",
            LastName = "Doe",
            Enabled = false,
            LastSyncedAt = syncedAt,
            TenantId = tenantId,
        };

        entry.ExternalUserId.ShouldBe("ext-123");
        entry.Username.ShouldBe("alice");
        entry.Email.ShouldBe("alice@test.com");
        entry.FirstName.ShouldBe("Alice");
        entry.LastName.ShouldBe("Doe");
        entry.Enabled.ShouldBeFalse();
        entry.LastSyncedAt.ShouldBe(syncedAt);
        entry.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void InheritsFromAuditedEntity()
    {
        FederatedIdentity entry = new();

        entry.ShouldBeAssignableTo<AuditedEntity>();
    }

    [Fact]
    public void ImplementsIMultiTenant()
    {
        FederatedIdentity entry = new();

        entry.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void TenantId_CanBeNull()
    {
        FederatedIdentity entry = new() { TenantId = null };

        entry.TenantId.ShouldBeNull();
    }

    [Fact]
    public void TenantId_CanBeSet()
    {
        var tenantId = Guid.NewGuid();
        FederatedIdentity entry = new() { TenantId = tenantId };

        entry.TenantId.ShouldBe(tenantId);
    }
}
