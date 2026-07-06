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
}
