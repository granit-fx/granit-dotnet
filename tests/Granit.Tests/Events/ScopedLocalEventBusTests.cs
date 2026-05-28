using Granit.Events;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Tests.Events;

public sealed class ScopedLocalEventBusTests
{
    private sealed record SampleEvent(string Value);

    [Fact]
    public async Task TryCreate_ReturnsNull_WhenScopeFactoryIsNull()
    {
        ScopedLocalEventBus.TryCreate(null).ShouldBeNull();
    }

    [Fact]
    public async Task TryCreate_ReturnsNull_WhenInnerBusNotRegistered()
    {
        await using ServiceProvider sp = new ServiceCollection().BuildServiceProvider();

        var wrapper = ScopedLocalEventBus.TryCreate(
            sp.GetRequiredService<IServiceScopeFactory>());

        wrapper.ShouldBeNull();
    }

    [Fact]
    public async Task TryCreate_ReturnsWrapper_WhenInnerBusIsRegistered()
    {
        ServiceCollection services = new();
        services.AddScoped(_ => Substitute.For<ILocalEventBus>());
        await using ServiceProvider sp = services.BuildServiceProvider();

        var wrapper = ScopedLocalEventBus.TryCreate(
            sp.GetRequiredService<IServiceScopeFactory>());

        wrapper.ShouldNotBeNull();
    }

    [Fact]
    public async Task PublishAsync_ResolvesInnerBusAndForwardsEvent()
    {
        ILocalEventBus inner = Substitute.For<ILocalEventBus>();
        ServiceCollection services = new();
        services.AddScoped(_ => inner);
        await using ServiceProvider sp = services.BuildServiceProvider();

        ScopedLocalEventBus wrapper = new(sp.GetRequiredService<IServiceScopeFactory>());
        SampleEvent evt = new("hello");

        await wrapper.PublishAsync(evt, TestContext.Current.CancellationToken);

        await inner.Received(1).PublishAsync(evt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_IsNoOp_WhenInnerBusUnregisteredAfterConstruction()
    {
        // Wrapper resolves the bus lazily per call; if DI evolves to drop the registration
        // (rare but possible in tests), publish should be a silent no-op rather than throw.
        await using ServiceProvider sp = new ServiceCollection().BuildServiceProvider();
        ScopedLocalEventBus wrapper = new(sp.GetRequiredService<IServiceScopeFactory>());

        await Should.NotThrowAsync(
            () => wrapper.PublishAsync(new SampleEvent("x"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_CreatesAFreshScopePerCall()
    {
        int created = 0;
        ServiceCollection services = new();
        services.AddScoped(_ =>
        {
            created++;
            return Substitute.For<ILocalEventBus>();
        });
        await using ServiceProvider sp = services.BuildServiceProvider();
        ScopedLocalEventBus wrapper = new(sp.GetRequiredService<IServiceScopeFactory>());

        await wrapper.PublishAsync(new SampleEvent("a"), TestContext.Current.CancellationToken);
        await wrapper.PublishAsync(new SampleEvent("b"), TestContext.Current.CancellationToken);

        created.ShouldBe(2);
    }

    [Fact]
    public async Task PublishAsync_PropagatesCancellationToken()
    {
        ILocalEventBus inner = Substitute.For<ILocalEventBus>();
        ServiceCollection services = new();
        services.AddScoped(_ => inner);
        await using ServiceProvider sp = services.BuildServiceProvider();
        ScopedLocalEventBus wrapper = new(sp.GetRequiredService<IServiceScopeFactory>());

        using CancellationTokenSource cts = new();

        await wrapper.PublishAsync(new SampleEvent("c"), cts.Token);

        await inner.Received(1).PublishAsync(Arg.Any<SampleEvent>(), cts.Token);
    }

    [Fact]
    public async Task PublishAsync_PropagatesInnerException()
    {
        ILocalEventBus inner = Substitute.For<ILocalEventBus>();
        inner.PublishAsync(Arg.Any<SampleEvent>(), Arg.Any<CancellationToken>())
             .ThrowsAsync(new InvalidOperationException("boom"));
        ServiceCollection services = new();
        services.AddScoped(_ => inner);
        await using ServiceProvider sp = services.BuildServiceProvider();
        ScopedLocalEventBus wrapper = new(sp.GetRequiredService<IServiceScopeFactory>());

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => wrapper.PublishAsync(new SampleEvent("err"), TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("boom");
    }
}
