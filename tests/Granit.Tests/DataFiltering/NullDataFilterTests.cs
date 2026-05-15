using Granit.DataFiltering;
using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Tests.DataFiltering;

public sealed class NullDataFilterTests
{
    [Fact]
    public void IsEnabled_IsAlwaysTrue()
    {
        NullDataFilter.Instance.IsEnabled<ISoftDeletable>().ShouldBeTrue();
        NullDataFilter.Instance.IsEnabled<IMultiTenant>().ShouldBeTrue();
        NullDataFilter.Instance.IsEnabled<IActive>().ShouldBeTrue();
    }

    [Fact]
    public void Disable_ReturnsDisposable_WithoutEffect()
    {
        using IDisposable scope = NullDataFilter.Instance.Disable<ISoftDeletable>();

        scope.ShouldNotBeNull();
        NullDataFilter.Instance.IsEnabled<ISoftDeletable>()
            .ShouldBeTrue("Disable is a no-op");
    }

    [Fact]
    public void Enable_ReturnsDisposable_WithoutEffect()
    {
        using IDisposable scope = NullDataFilter.Instance.Enable<ISoftDeletable>();

        scope.ShouldNotBeNull();
        NullDataFilter.Instance.IsEnabled<ISoftDeletable>().ShouldBeTrue();
    }

    [Fact]
    public void Instance_IsSingleton()
    {
        NullDataFilter a = NullDataFilter.Instance;
        NullDataFilter b = NullDataFilter.Instance;

        a.ShouldBeSameAs(b);
    }

    [Fact]
    public void ImplementsIDataFilter() =>
        NullDataFilter.Instance.ShouldBeAssignableTo<IDataFilter>();
}
