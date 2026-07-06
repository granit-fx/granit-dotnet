// =============================================================================
// TenantInfoTests - Unit tests for the TenantInfo record
// =============================================================================

using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class TenantInfoTests
{
    [Fact]
    public void Implements_ITenantInfo()
    {
        var info = new TenantInfo(Guid.NewGuid(), "Test");

        info.ShouldBeAssignableTo<ITenantInfo>();
        info.Id.ShouldBe(info.Id);
        info.Name.ShouldBe(info.Name);
    }
}
