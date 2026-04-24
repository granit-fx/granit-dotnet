using Granit.Metering.EntityFrameworkCore.Internal;
using Shouldly;
using Xunit;

namespace Granit.Metering.EntityFrameworkCore.Tests.Internal;

public sealed class MeteringConcurrencyLockTests
{
    [Fact]
    public void BuildResourceKey_HostOwned_ReturnsGlobalSuffix()
    {
        var meterId = Guid.Parse("00000000-0000-0000-0000-000000000abc");

        string key = MeteringConcurrencyLock.BuildResourceKey(meterId, tenantId: null);

        key.ShouldBe("granit.metering.aggregate:00000000000000000000000000000abc:global");
    }

    [Fact]
    public void BuildResourceKey_TenantOwned_IncludesTenantId()
    {
        var meterId = Guid.Parse("00000000-0000-0000-0000-000000000abc");
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000def");

        string key = MeteringConcurrencyLock.BuildResourceKey(meterId, tenantId);

        key.ShouldBe("granit.metering.aggregate:00000000000000000000000000000abc:00000000000000000000000000000def");
    }

    [Fact]
    public void BuildResourceKey_DifferentMetersOrTenants_AreDistinct()
    {
        var t = Guid.NewGuid();
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();

        string a = MeteringConcurrencyLock.BuildResourceKey(m1, t);
        string b = MeteringConcurrencyLock.BuildResourceKey(m2, t);
        string c = MeteringConcurrencyLock.BuildResourceKey(m1, null);

        a.ShouldNotBe(b);
        a.ShouldNotBe(c);
    }
}
