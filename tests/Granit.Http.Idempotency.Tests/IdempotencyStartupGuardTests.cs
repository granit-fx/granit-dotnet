// =============================================================================
// Tests - IdempotencyStartupGuard
// =============================================================================
// Environment × store × opt-in matrix for the fail-loud distributed-store guard.
// =============================================================================

using Granit.Caching;
using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Internal;
using Granit.Http.Idempotency.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.Tests;

public sealed class IdempotencyStartupGuardTests
{
    private static IdempotencyStartupGuard CreateGuard(
        IIdempotencyStore store,
        string environmentName,
        bool allowInMemoryStore = false)
    {
        ServiceCollection services = new();
        services.AddScoped(_ => store);
        ServiceProvider sp = services.BuildServiceProvider();

        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        return new IdempotencyStartupGuard(
            sp,
            Options.Create(new IdempotencyOptions { AllowInMemoryStore = allowInMemoryStore }),
            environment,
            NullLogger<IdempotencyStartupGuard>.Instance);
    }

    private static ConditionalCacheIdempotencyStore CreateConditionalStore(bool isDistributed)
    {
        IConditionalCache cache = Substitute.For<IConditionalCache>();
        cache.IsDistributed.Returns(isDistributed);
        return new ConditionalCacheIdempotencyStore(
            cache, NullLogger<ConditionalCacheIdempotencyStore>.Instance);
    }

    [Fact]
    public async Task InMemoryStore_InProduction_Throws()
    {
        IdempotencyStartupGuard guard = CreateGuard(CreateConditionalStore(isDistributed: false), Environments.Production);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => guard.StartAsync(CancellationToken.None));

        ex.Message.ShouldContain("AllowInMemoryStore");
    }

    [Fact]
    public async Task InMemoryStore_InDevelopment_Starts() =>
        await Should.NotThrowAsync(() =>
            CreateGuard(CreateConditionalStore(isDistributed: false), Environments.Development)
                .StartAsync(CancellationToken.None));

    [Fact]
    public async Task InMemoryStore_InProduction_WithOptIn_Starts() =>
        await Should.NotThrowAsync(() =>
            CreateGuard(CreateConditionalStore(isDistributed: false), Environments.Production, allowInMemoryStore: true)
                .StartAsync(CancellationToken.None));

    [Fact]
    public async Task DistributedStore_InProduction_Starts() =>
        await Should.NotThrowAsync(() =>
            CreateGuard(CreateConditionalStore(isDistributed: true), Environments.Production)
                .StartAsync(CancellationToken.None));

    [Fact]
    public async Task CustomStore_InProduction_SkipsEnforcement() =>
        await Should.NotThrowAsync(() =>
            CreateGuard(Substitute.For<IIdempotencyStore>(), Environments.Production)
                .StartAsync(CancellationToken.None));
}
