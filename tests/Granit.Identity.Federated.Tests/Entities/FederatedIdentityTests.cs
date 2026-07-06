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
}
