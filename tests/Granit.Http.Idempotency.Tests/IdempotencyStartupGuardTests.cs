// =============================================================================
// Tests - IdempotencyStartupGuard
// =============================================================================
// Environment × store × opt-in matrix for the fail-loud distributed-store guard.
// IsDistributed lives on the IIdempotencyStore contract, so the guard applies
// uniformly to built-in AND custom stores — there is no "unknown backend" case.
// =============================================================================

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

    private static IIdempotencyStore CreateStore(bool isDistributed, string backendName = "TestStore")
    {
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();
        store.IsDistributed.Returns(isDistributed);
        store.BackendName.Returns(backendName);
        return store;
    }

    [Fact]
    public async Task NonDistributedStore_InProduction_Throws()
    {
        IdempotencyStartupGuard guard = CreateGuard(CreateStore(isDistributed: false), Environments.Production);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => guard.StartAsync(CancellationToken.None));

        ex.Message.ShouldContain("AllowInMemoryStore");
        ex.Message.ShouldContain("Granit.Http.Idempotency.StackExchangeRedis");
    }

    [Fact]
    public async Task NonDistributedStore_InProduction_ExceptionNamesTheBackend()
    {
        IdempotencyStartupGuard guard = CreateGuard(
            CreateStore(isDistributed: false, backendName: "MyCustomStore"), Environments.Production);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => guard.StartAsync(CancellationToken.None));

        ex.Message.ShouldContain("MyCustomStore");
    }

    [Fact]
    public async Task NonDistributedStore_InDevelopment_Starts() =>
        await Should.NotThrowAsync(() =>
            CreateGuard(CreateStore(isDistributed: false), Environments.Development)
                .StartAsync(CancellationToken.None));

    [Fact]
    public async Task NonDistributedStore_InProduction_WithOptIn_Starts() =>
        await Should.NotThrowAsync(() =>
            CreateGuard(CreateStore(isDistributed: false), Environments.Production, allowInMemoryStore: true)
                .StartAsync(CancellationToken.None));

    [Fact]
    public async Task DistributedStore_InProduction_Starts() =>
        await Should.NotThrowAsync(() =>
            CreateGuard(CreateStore(isDistributed: true), Environments.Production)
                .StartAsync(CancellationToken.None));

    [Fact]
    public async Task InMemoryStore_InProduction_Throws()
    {
        // The shipped Development default must trip the guard in Production.
        InMemoryIdempotencyStore store = new(TimeProvider.System);

        IdempotencyStartupGuard guard = CreateGuard(store, Environments.Production);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => guard.StartAsync(CancellationToken.None));

        ex.Message.ShouldContain(nameof(InMemoryIdempotencyStore));
    }

    [Fact]
    public async Task CustomDistributedStore_InProduction_Starts() =>
        // A host-provided store that reports IsDistributed = true passes the guard —
        // the contract property is trusted, no type check involved.
        await Should.NotThrowAsync(() =>
            CreateGuard(CreateStore(isDistributed: true, backendName: "DynamoDbStore"), Environments.Production)
                .StartAsync(CancellationToken.None));
}
