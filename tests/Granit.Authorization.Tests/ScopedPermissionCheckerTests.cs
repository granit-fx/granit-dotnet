using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class ScopedPermissionCheckerTests
{
    private const string SamplePermission = "Invoices.Read";

    // --- TryCreate ---

    [Fact]
    public void TryCreate_ReturnsNull_WhenScopeFactoryIsNull() =>
        ScopedPermissionChecker.TryCreate(null).ShouldBeNull();

    [Fact]
    public async Task TryCreate_ReturnsNull_WhenInnerCheckerNotRegistered()
    {
        await using ServiceProvider sp = new ServiceCollection().BuildServiceProvider();

        var wrapper = ScopedPermissionChecker.TryCreate(
            sp.GetRequiredService<IServiceScopeFactory>());

        wrapper.ShouldBeNull();
    }

    [Fact]
    public async Task TryCreate_ReturnsWrapper_WhenInnerCheckerIsRegistered()
    {
        ServiceCollection services = new();
        services.AddScoped(_ => Substitute.For<IPermissionChecker>());
        await using ServiceProvider sp = services.BuildServiceProvider();

        var wrapper = ScopedPermissionChecker.TryCreate(
            sp.GetRequiredService<IServiceScopeFactory>());

        wrapper.ShouldNotBeNull();
    }

    // --- IsGrantedAsync ---

    [Fact]
    public async Task IsGrantedAsync_ReturnsTrue_WhenInnerGrants()
    {
        IPermissionChecker inner = Substitute.For<IPermissionChecker>();
        inner.IsGrantedAsync(SamplePermission, Arg.Any<CancellationToken>()).Returns(true);
        await using ServiceProvider sp = BuildProvider(inner);
        ScopedPermissionChecker wrapper = new(sp.GetRequiredService<IServiceScopeFactory>());

        bool granted = await wrapper.IsGrantedAsync(SamplePermission, TestContext.Current.CancellationToken);

        granted.ShouldBeTrue();
        await inner.Received(1).IsGrantedAsync(SamplePermission, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsGrantedAsync_ReturnsFalse_WhenInnerDenies()
    {
        IPermissionChecker inner = Substitute.For<IPermissionChecker>();
        inner.IsGrantedAsync(SamplePermission, Arg.Any<CancellationToken>()).Returns(false);
        await using ServiceProvider sp = BuildProvider(inner);
        ScopedPermissionChecker wrapper = new(sp.GetRequiredService<IServiceScopeFactory>());

        bool granted = await wrapper.IsGrantedAsync(SamplePermission, TestContext.Current.CancellationToken);

        granted.ShouldBeFalse();
    }

    [Fact]
    public async Task IsGrantedAsync_DeniesByDefault_WhenInnerUnregisteredAfterConstruction()
    {
        // Defensive least-privilege path: if the registration disappears after TryCreate
        // (caller built the wrapper directly), deny rather than fail-open.
        await using ServiceProvider sp = new ServiceCollection().BuildServiceProvider();
        ScopedPermissionChecker wrapper = new(sp.GetRequiredService<IServiceScopeFactory>());

        bool granted = await wrapper.IsGrantedAsync(SamplePermission, TestContext.Current.CancellationToken);

        granted.ShouldBeFalse();
    }

    [Fact]
    public async Task IsGrantedAsync_PropagatesCancellationToken()
    {
        IPermissionChecker inner = Substitute.For<IPermissionChecker>();
        await using ServiceProvider sp = BuildProvider(inner);
        ScopedPermissionChecker wrapper = new(sp.GetRequiredService<IServiceScopeFactory>());
        using CancellationTokenSource cts = new();

        await wrapper.IsGrantedAsync(SamplePermission, cts.Token);

        await inner.Received(1).IsGrantedAsync(SamplePermission, cts.Token);
    }

    [Fact]
    public async Task IsGrantedAsync_PropagatesInnerException()
    {
        IPermissionChecker inner = Substitute.For<IPermissionChecker>();
        inner.IsGrantedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .ThrowsAsync(new InvalidOperationException("boom"));
        await using ServiceProvider sp = BuildProvider(inner);
        ScopedPermissionChecker wrapper = new(sp.GetRequiredService<IServiceScopeFactory>());

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => wrapper.IsGrantedAsync(SamplePermission, TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("boom");
    }

    // --- GetGrantedAsync ---

    [Fact]
    public async Task GetGrantedAsync_ReturnsInnerSubset()
    {
        IPermissionChecker inner = Substitute.For<IPermissionChecker>();
        string[] requested = ["A", "B", "C"];
        inner.GetGrantedAsync(requested, Arg.Any<CancellationToken>())
             .Returns(new[] { "A", "C" });
        await using ServiceProvider sp = BuildProvider(inner);
        ScopedPermissionChecker wrapper = new(sp.GetRequiredService<IServiceScopeFactory>());

        IReadOnlyList<string> granted = await wrapper.GetGrantedAsync(
            requested, TestContext.Current.CancellationToken);

        granted.ShouldBe(["A", "C"]);
    }

    [Fact]
    public async Task GetGrantedAsync_ReturnsEmpty_WhenInnerUnregisteredAfterConstruction()
    {
        await using ServiceProvider sp = new ServiceCollection().BuildServiceProvider();
        ScopedPermissionChecker wrapper = new(sp.GetRequiredService<IServiceScopeFactory>());

        IReadOnlyList<string> granted = await wrapper.GetGrantedAsync(
            ["A"], TestContext.Current.CancellationToken);

        granted.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetGrantedAsync_PropagatesInnerException()
    {
        IPermissionChecker inner = Substitute.For<IPermissionChecker>();
        inner.GetGrantedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
             .ThrowsAsync(new InvalidOperationException("boom"));
        await using ServiceProvider sp = BuildProvider(inner);
        ScopedPermissionChecker wrapper = new(sp.GetRequiredService<IServiceScopeFactory>());

        await Should.ThrowAsync<InvalidOperationException>(
            () => wrapper.GetGrantedAsync(["A"], TestContext.Current.CancellationToken));
    }

    // --- Scope lifecycle ---

    [Fact]
    public async Task EachCall_CreatesAFreshScope()
    {
        int created = 0;
        ServiceCollection services = new();
        services.AddScoped(_ =>
        {
            created++;
            return Substitute.For<IPermissionChecker>();
        });
        await using ServiceProvider sp = services.BuildServiceProvider();
        ScopedPermissionChecker wrapper = new(sp.GetRequiredService<IServiceScopeFactory>());

        await wrapper.IsGrantedAsync(SamplePermission, TestContext.Current.CancellationToken);
        await wrapper.GetGrantedAsync(["A"], TestContext.Current.CancellationToken);

        created.ShouldBe(2);
    }

    private static ServiceProvider BuildProvider(IPermissionChecker inner)
    {
        ServiceCollection services = new();
        services.AddScoped(_ => inner);
        return services.BuildServiceProvider();
    }
}
