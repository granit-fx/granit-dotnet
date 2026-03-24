using Granit.MultiTenancy;
using Shouldly;
using Xunit;

namespace Granit.Tests.MultiTenancy;

public sealed class NullTenantContextTests
{
    [Fact]
    public void IsAvailable_IsFalse() =>
        NullTenantContext.Instance.IsAvailable.ShouldBeFalse();

    [Fact]
    public void Id_IsNull() =>
        NullTenantContext.Instance.Id.ShouldBeNull();

    [Fact]
    public void Name_IsNull() =>
        NullTenantContext.Instance.Name.ShouldBeNull();

    [Fact]
    public void Change_ReturnsDisposable_WithoutEffect()
    {
        using IDisposable scope = NullTenantContext.Instance.Change(Guid.NewGuid(), "tenant-a");

        scope.ShouldNotBeNull();
        NullTenantContext.Instance.Id.ShouldBeNull("Change is a no-op");
        NullTenantContext.Instance.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public void Change_WithNull_ReturnsDisposable()
    {
        using IDisposable scope = NullTenantContext.Instance.Change(null);

        scope.ShouldNotBeNull();
    }

    [Fact]
    public void Instance_IsSingleton()
    {
        NullTenantContext a = NullTenantContext.Instance;
        NullTenantContext b = NullTenantContext.Instance;

        a.ShouldBeSameAs(b);
    }

    [Fact]
    public void ImplementsICurrentTenant() =>
        NullTenantContext.Instance.ShouldBeAssignableTo<ICurrentTenant>();
}
