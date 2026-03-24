using Granit.Authentication.JwtBearer.EntraId.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.JwtBearer.EntraId.Tests;

public sealed class EntraIdOptionsTests
{
    [Fact]
    public void SectionName_IsEntraId() =>
        EntraIdOptions.SectionName.ShouldBe("EntraId");

    [Fact]
    public void Defaults_AreCorrect()
    {
        EntraIdOptions options = new();

        options.Instance.ShouldBe("https://login.microsoftonline.com/");
        options.TenantId.ShouldBe(string.Empty);
        options.ClientId.ShouldBe(string.Empty);
        options.RequireHttpsMetadata.ShouldBeTrue();
        options.AdminRole.ShouldBe("admin");
    }

    [Fact]
    public void Authority_IsDerivedFromInstanceAndTenantId()
    {
        EntraIdOptions options = new()
        {
            Instance = "https://login.microsoftonline.com/",
            TenantId = "00000000-0000-0000-0000-000000000001",
        };

        options.Authority.ShouldBe("https://login.microsoftonline.com/00000000-0000-0000-0000-000000000001/v2.0");
    }

    [Fact]
    public void Authority_TrimsTrailingSlashFromInstance()
    {
        EntraIdOptions options = new()
        {
            Instance = "https://login.microsoftonline.com/",
            TenantId = "my-tenant",
        };

        options.Authority.ShouldBe("https://login.microsoftonline.com/my-tenant/v2.0");
    }

    [Fact]
    public void Authority_HandlesInstanceWithoutTrailingSlash()
    {
        EntraIdOptions options = new()
        {
            Instance = "https://login.microsoftonline.com",
            TenantId = "my-tenant",
        };

        options.Authority.ShouldBe("https://login.microsoftonline.com/my-tenant/v2.0");
    }

    [Fact]
    public void AdminRole_CanBeOverridden()
    {
        EntraIdOptions options = new() { AdminRole = "GlobalAdmin" };

        options.AdminRole.ShouldBe("GlobalAdmin");
    }
}
