using Granit.Authorization.Services;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

/// <summary>
/// Pins the tag contract consumed by <see cref="Cache.RoleCacheInvalidationHandler"/>:
/// every role-scope grant cache entry MUST carry the <c>role:{name}</c> tag so
/// role-event handlers can invalidate stale entries with a single
/// <c>RemoveByTagAsync</c> call.
/// </summary>
public sealed class PermissionCheckerCacheTagTests
{
    [Fact]
    public void BuildCacheTags_RoleProvider_ReturnsRoleTag()
    {
        IEnumerable<string>? tags = PermissionChecker.BuildCacheTags(
            PermissionGrantProviderNames.Role, providerKey: "Manager");

        tags.ShouldNotBeNull();
        tags.ShouldBe(["role:Manager"]);
    }

    [Fact]
    public void BuildCacheTags_UserProvider_ReturnsNull()
    {
        IEnumerable<string>? tags = PermissionChecker.BuildCacheTags(
            PermissionGrantProviderNames.User, providerKey: "user-42");

        tags.ShouldBeNull();
    }

    [Fact]
    public void BuildCacheTags_ClientProvider_ReturnsNull()
    {
        IEnumerable<string>? tags = PermissionChecker.BuildCacheTags(
            PermissionGrantProviderNames.Client, providerKey: "admin-spa");

        tags.ShouldBeNull();
    }

    [Fact]
    public void RoleTag_FormatStable() => PermissionChecker.RoleTag("X").ShouldBe("role:X");
}
