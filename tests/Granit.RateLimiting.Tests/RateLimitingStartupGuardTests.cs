// =============================================================================
// Tests - RateLimitingStartupGuard
// =============================================================================
// Environment × store × opt-in matrix for the fail-loud counter-store guard.
// =============================================================================

using Granit.RateLimiting.Abstractions;
using Granit.RateLimiting.Internal;
using Granit.RateLimiting.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Tests;

public sealed class RateLimitingStartupGuardTests
{
    private static RateLimitingStartupGuard CreateGuard(
        IRateLimitCounterStore store,
        string environmentName,
        bool allowInMemoryCounterStore = false,
        bool enabled = true)
    {
        ServiceCollection services = new();
        services.AddScoped(_ => store);
        ServiceProvider sp = services.BuildServiceProvider();

        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        return new RateLimitingStartupGuard(
            sp,
            Microsoft.Extensions.Options.Options.Create(new GranitRateLimitingOptions
            {
                Enabled = enabled,
                AllowInMemoryCounterStore = allowInMemoryCounterStore,
            }),
            environment,
            NullLogger<RateLimitingStartupGuard>.Instance);
    }

    private static InMemoryRateLimitCounterStore CreateInMemoryStore() => new(TimeProvider.System);

    [Fact]
    public async Task InMemoryStore_InProduction_Throws()
    {
        RateLimitingStartupGuard guard = CreateGuard(CreateInMemoryStore(), Environments.Production);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => guard.StartAsync(CancellationToken.None));

        ex.Message.ShouldContain("AllowInMemoryCounterStore");
    }

    [Fact]
    public async Task InMemoryStore_InDevelopment_Starts() =>
        await Should.NotThrowAsync(() =>
            CreateGuard(CreateInMemoryStore(), Environments.Development)
                .StartAsync(CancellationToken.None));

    [Fact]
    public async Task InMemoryStore_InProduction_WithOptIn_Starts() =>
        await Should.NotThrowAsync(() =>
            CreateGuard(CreateInMemoryStore(), Environments.Production, allowInMemoryCounterStore: true)
                .StartAsync(CancellationToken.None));

    [Fact]
    public async Task DistributedStore_InProduction_Starts() =>
        await Should.NotThrowAsync(() =>
            CreateGuard(Substitute.For<IRateLimitCounterStore>(), Environments.Production)
                .StartAsync(CancellationToken.None));

    [Fact]
    public async Task Disabled_InProduction_WithInMemoryStore_Starts() =>
        await Should.NotThrowAsync(() =>
            CreateGuard(CreateInMemoryStore(), Environments.Production, enabled: false)
                .StartAsync(CancellationToken.None));
}
