using Granit.Domain;
using Granit.Identity.Local.Domain;
using Granit.OpenIddict.Domain;
using Granit.OpenIddict.Entities.OpenIddict;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Entities;

public sealed class EntityTests
{
    [Fact]
    public void GranitUser_Default_Values()
    {
        LocalIdentity user = new();

        user.FirstName.ShouldBeNull();
        user.LastName.ShouldBeNull();
        user.TenantId.ShouldBeNull();
        user.IsDeleted.ShouldBeFalse();
        user.DeletedAt.ShouldBeNull();
        user.DeletedBy.ShouldBeNull();
        user.CustomAttributesJson.ShouldBeNull();
        user.CreatedAt.ShouldBe(default);
        user.CreatedBy.ShouldBe(string.Empty);
        user.ModifiedAt.ShouldBeNull();
        user.ModifiedBy.ShouldBeNull();
    }

    [Fact]
    public void GranitUser_Implements_IMultiTenant()
    {
        LocalIdentity user = new();

        user.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void GranitRole_Default_Values()
    {
        GranitRole role = new();

        role.Description.ShouldBeNull();
    }

    [Fact]
    public void GranitUserGroup_Default_Values()
    {
        GranitUserGroup group = new();

        group.Name.ShouldBe(string.Empty);
        group.Description.ShouldBeNull();
        group.TenantId.ShouldBeNull();
    }

    [Fact]
    public void GranitUserGroup_Implements_IMultiTenant()
    {
        GranitUserGroup group = new();

        group.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void GranitUserGroupMember_Default_Values()
    {
        GranitUserGroupMember member = new();

        member.GroupId.ShouldBe(Guid.Empty);
        member.UserId.ShouldBe(Guid.Empty);
        member.TenantId.ShouldBeNull();
    }

    [Fact]
    public void GranitUserGroupMember_Implements_IMultiTenant()
    {
        GranitUserGroupMember member = new();

        member.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void SigningKey_Create_Factory()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var key = SigningKey.Create(
            "key-001", "signing", "RS256", "encrypted-data",
            activatedAt: now, expiresAt: now.AddDays(90));

        key.KeyId.ShouldBe("key-001");
        key.KeyType.ShouldBe("signing");
        key.Algorithm.ShouldBe("RS256");
        key.EncryptedKeyMaterial.ShouldBe("encrypted-data");
        key.Status.ShouldBe(SigningKeyStatus.Active);
        key.ActivatedAt.ShouldBe(now);
        key.ExpiresAt.ShouldBe(now.AddDays(90));
        key.RetiredAt.ShouldBeNull();
        key.KeySize.ShouldBe(2048);
    }

    [Fact]
    public void SigningKey_Retire_And_Revoke()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var key = SigningKey.Create(
            "key-002", "encryption", "RSA-OAEP", "encrypted-data",
            activatedAt: now.AddDays(-90), expiresAt: now, keySize: 4096);

        key.Retire(now);
        key.Status.ShouldBe(SigningKeyStatus.Retired);
        key.RetiredAt.ShouldBe(now);
        key.KeySize.ShouldBe(4096);

        key.Revoke();
        key.Status.ShouldBe(SigningKeyStatus.Revoked);
    }

    [Fact]
    public void SigningKeyStatus_Has_Expected_Values()
    {
        ((int)SigningKeyStatus.Active).ShouldBe(0);
        ((int)SigningKeyStatus.Retired).ShouldBe(1);
        ((int)SigningKeyStatus.Revoked).ShouldBe(2);
    }

    [Fact]
    public void GranitOpenIddictApplication_Implements_IMultiTenant()
    {
        GranitOpenIddictApplication application = new();

        application.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void GranitOpenIddictApplication_TenantId_Default_Is_Null()
    {
        GranitOpenIddictApplication application = new();

        application.TenantId.ShouldBeNull();
    }

    [Fact]
    public void GranitOpenIddictAuthorization_Implements_IMultiTenant()
    {
        GranitOpenIddictAuthorization authorization = new();

        authorization.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void GranitOpenIddictAuthorization_TenantId_Default_Is_Null()
    {
        GranitOpenIddictAuthorization authorization = new();

        authorization.TenantId.ShouldBeNull();
    }

    [Fact]
    public void GranitOpenIddictAuthorization_TenantId_Setter()
    {
        var tenantId = Guid.NewGuid();
        GranitOpenIddictAuthorization authorization = new() { TenantId = tenantId };

        authorization.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void GranitOpenIddictScope_Implements_IMultiTenant()
    {
        GranitOpenIddictScope scope = new();

        scope.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void GranitOpenIddictScope_TenantId_Default_Is_Null()
    {
        GranitOpenIddictScope scope = new();

        scope.TenantId.ShouldBeNull();
    }

    [Fact]
    public void GranitOpenIddictScope_TenantId_Setter()
    {
        var tenantId = Guid.NewGuid();
        GranitOpenIddictScope scope = new() { TenantId = tenantId };

        scope.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void GranitOpenIddictToken_Implements_IMultiTenant()
    {
        GranitOpenIddictToken token = new();

        token.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void GranitOpenIddictToken_TenantId_Default_Is_Null()
    {
        GranitOpenIddictToken token = new();

        token.TenantId.ShouldBeNull();
    }

    [Fact]
    public void GranitOpenIddictToken_TenantId_Setter()
    {
        var tenantId = Guid.NewGuid();
        GranitOpenIddictToken token = new() { TenantId = tenantId };

        token.TenantId.ShouldBe(tenantId);
    }
}
