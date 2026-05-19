using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class CurrentTenantExtensionsTests
{
    private static CurrentTenant Create() => new();

    [Fact]
    public void ChangeToHost_SwitchesToNullTenantContext()
    {
        CurrentTenant tenant = Create();
        var tenantId = Guid.NewGuid();
        tenant.Change(tenantId, "Acme");

        using (tenant.ChangeToHost())
        {
            tenant.IsAvailable.ShouldBeFalse();
            tenant.Id.ShouldBeNull();
            tenant.Name.ShouldBeNull();
        }

        // Restored after disposal
        tenant.IsAvailable.ShouldBeTrue();
        tenant.Id.ShouldBe(tenantId);
        tenant.Name.ShouldBe("Acme");
    }

    [Fact]
    public void ChangeToHost_WhenAlreadyHost_NoOp()
    {
        CurrentTenant tenant = Create();

        using (tenant.ChangeToHost())
        {
            tenant.IsAvailable.ShouldBeFalse();
            tenant.Id.ShouldBeNull();
        }

        tenant.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public void ChangeToHost_ThrowsOnNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            CurrentTenantExtensions.ChangeToHost(null!));
    }
}
