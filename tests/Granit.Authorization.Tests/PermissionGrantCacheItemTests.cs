using Granit.Authorization.Cache;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class PermissionGrantCacheItemTests
{
    [Fact]
    public void IsGranted_DefaultValue_IsFalse()
    {
        PermissionGrantCacheItem item = new();

        item.IsGranted.ShouldBeFalse();
    }

    [Fact]
    public void IsGranted_SetToTrue_ReturnsTrue()
    {
        PermissionGrantCacheItem item = new() { IsGranted = true };

        item.IsGranted.ShouldBeTrue();
    }

    [Fact]
    public void IsGranted_SetToFalse_ReturnsFalse()
    {
        PermissionGrantCacheItem item = new() { IsGranted = false };

        item.IsGranted.ShouldBeFalse();
    }
}
