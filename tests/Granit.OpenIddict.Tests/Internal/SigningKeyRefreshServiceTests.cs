using Granit.OpenIddict.Domain;
using Granit.OpenIddict.Internal;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using OpenIddict.Server;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Internal;

/// <summary>
/// The refresh service evicts the cached OpenIddict options only when the active/retired key set
/// changes since the last check, and survives a failed key-store read (schema absent, transient DB
/// error) without invalidating.
/// </summary>
public sealed class SigningKeyRefreshServiceTests
{
    [Fact]
    public async Task RefreshIfChangedAsync_KeySetUnchanged_DoesNotInvalidate()
    {
        FakeSigningKeyStore store = new([Key("a", SigningKeyStatus.Active)]);
        RecordingOptionsCache cache = new();
        SigningKeyRefreshService service = Build(store, cache);

        await service.PrimeAsync(TestContext.Current.CancellationToken);
        await service.RefreshIfChangedAsync(TestContext.Current.CancellationToken);

        cache.RemoveCount.ShouldBe(0);
    }

    [Fact]
    public async Task RefreshIfChangedAsync_KeyRotatedIn_InvalidatesCache()
    {
        FakeSigningKeyStore store = new([Key("a", SigningKeyStatus.Active)]);
        RecordingOptionsCache cache = new();
        SigningKeyRefreshService service = Build(store, cache);

        await service.PrimeAsync(TestContext.Current.CancellationToken);

        // The rotation job minted a new active key on another node and retired the old one.
        store.Keys = [Key("a", SigningKeyStatus.Retired), Key("b", SigningKeyStatus.Active)];
        await service.RefreshIfChangedAsync(TestContext.Current.CancellationToken);

        cache.RemoveCount.ShouldBe(1);
    }

    [Fact]
    public async Task RefreshIfChangedAsync_OnlyStatusChanged_InvalidatesCache()
    {
        FakeSigningKeyStore store = new([Key("a", SigningKeyStatus.Active)]);
        RecordingOptionsCache cache = new();
        SigningKeyRefreshService service = Build(store, cache);

        await service.PrimeAsync(TestContext.Current.CancellationToken);

        store.Keys = [Key("a", SigningKeyStatus.Retired)]; // same key, now retired
        await service.RefreshIfChangedAsync(TestContext.Current.CancellationToken);

        cache.RemoveCount.ShouldBe(1);
    }

    [Fact]
    public async Task RefreshIfChangedAsync_StoreThrows_IsNoOp()
    {
        FakeSigningKeyStore store = new([Key("a", SigningKeyStatus.Active)]) { Throw = true };
        RecordingOptionsCache cache = new();
        SigningKeyRefreshService service = Build(store, cache);

        await service.RefreshIfChangedAsync(TestContext.Current.CancellationToken);

        cache.RemoveCount.ShouldBe(0);
    }

    [Fact]
    public async Task EnsureInitializedAsync_EmptyStore_GeneratesAndReloads()
    {
        FakeSigningKeyStore store = new([]); // fresh deployment, no keys
        RecordingOptionsCache cache = new();
        IKeyRotationService rotation = Substitute.For<IKeyRotationService>();
        rotation.RotateAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                store.Keys = [Key("a", SigningKeyStatus.Active)]; // generation persisted the key
                return new KeyRotationResult(KeysGenerated: 2, KeysRetired: 0, KeysRevoked: 0, KeysPruned: 0);
            });
        SigningKeyRefreshService service = Build(store, cache, rotation);

        await service.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        await rotation.Received(1).RotateAsync(Arg.Any<CancellationToken>());
        cache.RemoveCount.ShouldBe(1, "freshly minted credentials must be loaded immediately");
    }

    [Fact]
    public async Task EnsureInitializedAsync_LostGenerationRace_ReloadsPeerKeys()
    {
        FakeSigningKeyStore store = new([]); // empty at startup
        RecordingOptionsCache cache = new();
        IKeyRotationService rotation = Substitute.For<IKeyRotationService>();

        // A peer wins the race: it persisted the active key, then our duplicate insert is rejected.
        rotation.RotateAsync(Arg.Any<CancellationToken>())
            .Returns<KeyRotationResult>(_ =>
            {
                store.Keys = [Key("peer", SigningKeyStatus.Active)];
                throw new InvalidOperationException("duplicate key value violates unique constraint");
            });
        SigningKeyRefreshService service = Build(store, cache, rotation);

        await service.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // The race loss is swallowed, but the peer's keys must still be loaded — not left ephemeral.
        cache.RemoveCount.ShouldBe(1, "a replica that lost the race must still load the peer's keys");
    }

    [Fact]
    public async Task EnsureInitializedAsync_KeysAlreadyPresent_DoesNotGenerate()
    {
        FakeSigningKeyStore store = new([Key("a", SigningKeyStatus.Active)]);
        RecordingOptionsCache cache = new();
        IKeyRotationService rotation = Substitute.For<IKeyRotationService>();
        SigningKeyRefreshService service = Build(store, cache, rotation);

        await service.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        await rotation.DidNotReceive().RotateAsync(Arg.Any<CancellationToken>());
        cache.RemoveCount.ShouldBe(0);
    }

    [Fact]
    public async Task EnsureInitializedAsync_StoreThrows_IsNoOp()
    {
        FakeSigningKeyStore store = new([]) { Throw = true }; // schema absent on first boot
        RecordingOptionsCache cache = new();
        IKeyRotationService rotation = Substitute.For<IKeyRotationService>();
        SigningKeyRefreshService service = Build(store, cache, rotation);

        await service.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        await rotation.DidNotReceive().RotateAsync(Arg.Any<CancellationToken>());
        cache.RemoveCount.ShouldBe(0);
    }

    private static SigningKeyRefreshService Build(
        ISigningKeyStore store,
        IOptionsMonitorCache<OpenIddictServerOptions> cache,
        IKeyRotationService? rotation = null)
    {
        ServiceProvider provider = new ServiceCollection()
            .AddScoped(_ => store)
            .AddScoped(_ => rotation ?? Substitute.For<IKeyRotationService>())
            .BuildServiceProvider();

        IOptionsMonitor<OpenIddictServerOptions> monitor =
            Substitute.For<IOptionsMonitor<OpenIddictServerOptions>>();
        monitor.Get(Arg.Any<string>()).Returns(new OpenIddictServerOptions());

        return new SigningKeyRefreshService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            cache,
            monitor,
            Microsoft.Extensions.Options.Options.Create(new GranitKeyRotationOptions { Enabled = true }),
            TimeProvider.System,
            NullLogger<SigningKeyRefreshService>.Instance);
    }

    private static SigningKey Key(string keyId, SigningKeyStatus status)
    {
        var key = SigningKey.Create(
            keyId, "signing", "RS256", "encrypted",
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(90));
        if (status == SigningKeyStatus.Retired)
        {
            key.Retire(DateTimeOffset.UnixEpoch.AddDays(80));
        }

        return key;
    }

    private sealed class FakeSigningKeyStore(IReadOnlyList<SigningKey> keys) : ISigningKeyStore
    {
        public IReadOnlyList<SigningKey> Keys { get; set; } = keys;

        public bool Throw { get; set; }

        public Task<IReadOnlyList<SigningKey>> GetKeysAsync(
            SigningKeyStatus[] statuses, CancellationToken cancellationToken = default) =>
            Throw
                ? throw new InvalidOperationException("relation \"openiddict_signing_keys\" does not exist")
                : Task.FromResult(Keys);

        public Task<IReadOnlyList<SigningKey>> GetKeysAsync(params SigningKeyStatus[] statuses) =>
            GetKeysAsync(statuses, default);

        public Task<SigningKey?> GetActiveKeyAsync(string keyType, CancellationToken cancellationToken = default) =>
            Throw
                ? throw new InvalidOperationException("relation \"openiddict_signing_keys\" does not exist")
                : Task.FromResult(Keys.FirstOrDefault(
                    k => k.KeyType == keyType && k.Status == SigningKeyStatus.Active));

        public Task CreateAsync(SigningKey key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> UpdateAsync(SigningKey key, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<int> PruneRevokedAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class RecordingOptionsCache : IOptionsMonitorCache<OpenIddictServerOptions>
    {
        public int RemoveCount { get; private set; }

        public bool TryRemove(string? name)
        {
            RemoveCount++;
            return true;
        }

        public OpenIddictServerOptions GetOrAdd(string? name, Func<OpenIddictServerOptions> createOptions) =>
            createOptions();

        public bool TryAdd(string? name, OpenIddictServerOptions options) => true;

        public void Clear() { }
    }
}
