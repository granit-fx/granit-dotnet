using Granit.Core.Domain;
using Granit.OpenIddict.Domain;
using Granit.OpenIddict.Entities;
using Granit.OpenIddict.Entities.OpenIddict;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Entities;

public sealed class EntityTests
{
    [Fact]
    public void GranitUser_Default_Values()
    {
        GranitUser user = new();

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
    public void GranitUser_Property_Setters()
    {
        var tenantId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        GranitUser user = new()
        {
            FirstName = "Alice",
            LastName = "Doe",
            TenantId = tenantId,
            IsDeleted = true,
            DeletedAt = now,
            DeletedBy = "admin",
            CustomAttributesJson = """{"key":"value"}""",
            CreatedAt = now,
            CreatedBy = "system",
            ModifiedAt = now,
            ModifiedBy = "admin",
        };

        user.FirstName.ShouldBe("Alice");
        user.LastName.ShouldBe("Doe");
        user.TenantId.ShouldBe(tenantId);
        user.IsDeleted.ShouldBeTrue();
        user.DeletedAt.ShouldBe(now);
        user.DeletedBy.ShouldBe("admin");
        user.CustomAttributesJson.ShouldBe("""{"key":"value"}""");
        user.CreatedAt.ShouldBe(now);
        user.CreatedBy.ShouldBe("system");
        user.ModifiedAt.ShouldBe(now);
        user.ModifiedBy.ShouldBe("admin");
    }

    [Fact]
    public void GranitUser_Implements_IMultiTenant()
    {
        GranitUser user = new();

        user.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void GranitUser_IMultiTenant_TenantId()
    {
        var tenantId = Guid.NewGuid();
        GranitUser user = new() { TenantId = tenantId };

        ((IMultiTenant)user).TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void GranitRole_Default_Values()
    {
        GranitRole role = new();

        role.Description.ShouldBeNull();
    }

    [Fact]
    public void GranitRole_Description_Setter()
    {
        GranitRole role = new() { Description = "Administrator role" };

        role.Description.ShouldBe("Administrator role");
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
    public void GranitUserGroup_Property_Setters()
    {
        var tenantId = Guid.NewGuid();
        GranitUserGroup group = new()
        {
            Name = "Developers",
            Description = "Development team",
            TenantId = tenantId,
        };

        group.Name.ShouldBe("Developers");
        group.Description.ShouldBe("Development team");
        group.TenantId.ShouldBe(tenantId);
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
    public void GranitUserGroupMember_Property_Setters()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        GranitUserGroupMember member = new()
        {
            GroupId = groupId,
            UserId = userId,
            TenantId = tenantId,
        };

        member.GroupId.ShouldBe(groupId);
        member.UserId.ShouldBe(userId);
        member.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void SigningKey_Default_Values()
    {
        SigningKey key = new();

        key.KeyId.ShouldBe(string.Empty);
        key.KeyType.ShouldBe("signing");
        key.Algorithm.ShouldBe("RS256");
        key.EncryptedKeyMaterial.ShouldBe(string.Empty);
        key.Status.ShouldBe(SigningKeyStatus.Active);
        key.ActivatedAt.ShouldBe(default);
        key.ExpiresAt.ShouldBe(default);
        key.RetiredAt.ShouldBeNull();
        key.KeySize.ShouldBe(2048);
    }

    [Fact]
    public void SigningKey_Property_Setters()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SigningKey key = new()
        {
            KeyId = "key-001",
            KeyType = "encryption",
            Algorithm = "RSA-OAEP",
            EncryptedKeyMaterial = "encrypted-data",
            Status = SigningKeyStatus.Retired,
            ActivatedAt = now.AddDays(-90),
            ExpiresAt = now,
            RetiredAt = now,
            KeySize = 4096,
        };

        key.KeyId.ShouldBe("key-001");
        key.KeyType.ShouldBe("encryption");
        key.Algorithm.ShouldBe("RSA-OAEP");
        key.EncryptedKeyMaterial.ShouldBe("encrypted-data");
        key.Status.ShouldBe(SigningKeyStatus.Retired);
        key.RetiredAt.ShouldBe(now);
        key.KeySize.ShouldBe(4096);
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
    public void GranitOpenIddictApplication_TenantId_Setter()
    {
        var tenantId = Guid.NewGuid();
        GranitOpenIddictApplication application = new() { TenantId = tenantId };

        application.TenantId.ShouldBe(tenantId);
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
