// =============================================================================
// InMemoryEntityEncryptionKeyStoreTests - In-memory per-entity key store
// =============================================================================
// Verifies:
//   - GetOrCreateKeyAsync creates a new key on first call, returns same on second
//   - Generated keys are 32 bytes (AES-256)
//   - GetKeyAsync returns null when key does not exist
//   - GetKeyAsync returns the key when it exists
//   - DeleteKeyAsync removes an existing key
//   - DeleteKeyAsync is a no-op when key does not exist
//   - KeyExistsAsync returns correct boolean
//   - Different entity types with same ID produce different keys
//   - Thread safety under concurrent access
// =============================================================================

using Granit.Encryption.Services;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Encryption.Tests;

public sealed class InMemoryEntityEncryptionKeyStoreTests
{
    private readonly InMemoryEntityEncryptionKeyStore _sut = new(DevelopmentEnvironment());

    private static IHostEnvironment DevelopmentEnvironment()
    {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Development);
        return env;
    }

    private static IHostEnvironment EnvironmentNamed(string name)
    {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(name);
        return env;
    }

    // ──── Production guard (fails closed) ────

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("QA")]
    public void Constructor_ThrowsOutsideDevelopment(string environmentName)
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => new InMemoryEntityEncryptionKeyStore(EnvironmentNamed(environmentName)));

        ex.Message.ShouldContain("Development");
        ex.Message.ShouldContain("Vault");
        ex.Message.ShouldContain(environmentName);
    }

    [Fact]
    public void Constructor_DoesNotThrow_InDevelopment() =>
        Should.NotThrow(() => new InMemoryEntityEncryptionKeyStore(EnvironmentNamed(Environments.Development)));

    // ──── GetOrCreateKeyAsync ────

    [Fact]
    public async Task GetOrCreateKeyAsync_CreatesNewKey_WhenKeyDoesNotExist()
    {
        byte[] key = await _sut.GetOrCreateKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);

        key.ShouldNotBeNull();
        key.Length.ShouldBe(32, "AES-256 keys must be 32 bytes");
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_ReturnsSameKey_OnSecondCall()
    {
        byte[] first = await _sut.GetOrCreateKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);
        byte[] second = await _sut.GetOrCreateKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_ReturnsDifferentKeys_ForDifferentEntityIds()
    {
        byte[] key1 = await _sut.GetOrCreateKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);
        byte[] key2 = await _sut.GetOrCreateKeyAsync("Patient", "id-2", TestContext.Current.CancellationToken);

        key1.ShouldNotBeSameAs(key2);
        key1.ShouldNotBe(key2);
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_ReturnsDifferentKeys_ForDifferentEntityTypes()
    {
        byte[] patientKey = await _sut.GetOrCreateKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);
        byte[] orderKey = await _sut.GetOrCreateKeyAsync("Order", "id-1", TestContext.Current.CancellationToken);

        patientKey.ShouldNotBeSameAs(orderKey);
        patientKey.ShouldNotBe(orderKey);
    }

    // ──── GetKeyAsync ────

    [Fact]
    public async Task GetKeyAsync_ReturnsNull_WhenKeyDoesNotExist()
    {
        byte[]? key = await _sut.GetKeyAsync("Patient", "nonexistent", TestContext.Current.CancellationToken);

        key.ShouldBeNull();
    }

    [Fact]
    public async Task GetKeyAsync_ReturnsKey_WhenKeyExists()
    {
        byte[] created = await _sut.GetOrCreateKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);

        byte[]? retrieved = await _sut.GetKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);

        retrieved.ShouldNotBeNull();
        retrieved.ShouldBeSameAs(created);
    }

    // ──── DeleteKeyAsync ────

    [Fact]
    public async Task DeleteKeyAsync_RemovesExistingKey()
    {
        await _sut.GetOrCreateKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);

        await _sut.DeleteKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);

        byte[]? key = await _sut.GetKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);
        key.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteKeyAsync_IsNoOp_WhenKeyDoesNotExist()
    {
        await Should.NotThrowAsync(
            () => _sut.DeleteKeyAsync("Patient", "nonexistent", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteKeyAsync_OnlyAffectsTargetKey()
    {
        await _sut.GetOrCreateKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);
        await _sut.GetOrCreateKeyAsync("Patient", "id-2", TestContext.Current.CancellationToken);

        await _sut.DeleteKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);

        (await _sut.GetKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken)).ShouldBeNull();
        (await _sut.GetKeyAsync("Patient", "id-2", TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    // ──── KeyExistsAsync ────

    [Fact]
    public async Task KeyExistsAsync_ReturnsFalse_WhenKeyDoesNotExist()
    {
        bool exists = await _sut.KeyExistsAsync("Patient", "nonexistent", TestContext.Current.CancellationToken);

        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task KeyExistsAsync_ReturnsTrue_WhenKeyExists()
    {
        await _sut.GetOrCreateKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);

        bool exists = await _sut.KeyExistsAsync("Patient", "id-1", TestContext.Current.CancellationToken);

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task KeyExistsAsync_ReturnsFalse_AfterDeletion()
    {
        await _sut.GetOrCreateKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);
        await _sut.DeleteKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);

        bool exists = await _sut.KeyExistsAsync("Patient", "id-1", TestContext.Current.CancellationToken);

        exists.ShouldBeFalse();
    }

    // ──── GetOrCreateKeyAsync after delete ────

    [Fact]
    public async Task GetOrCreateKeyAsync_CreatesNewKey_AfterDeletion()
    {
        byte[] original = await _sut.GetOrCreateKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);
        await _sut.DeleteKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);

        byte[] recreated = await _sut.GetOrCreateKeyAsync("Patient", "id-1", TestContext.Current.CancellationToken);

        recreated.ShouldNotBeNull();
        recreated.Length.ShouldBe(32);
        // The original key was zeroed, so a comparison of values would not be reliable.
        // We verify that a new key reference is returned.
        recreated.ShouldNotBeSameAs(original);
    }

    // ──── Thread safety ────

    [Fact]
    public async Task GetOrCreateKeyAsync_ConcurrentAccess_ReturnsSameKeyForSameEntity()
    {
        const int concurrency = 50;

        Task<byte[]>[] tasks = Enumerable.Range(0, concurrency)
            .Select(_ => _sut.GetOrCreateKeyAsync("Patient", "concurrent-id", TestContext.Current.CancellationToken))
            .ToArray();

        byte[][] results = await Task.WhenAll(tasks);

        // All tasks should return the exact same key reference
        byte[] expected = results[0];
        foreach (byte[] key in results)
        {
            key.ShouldBeSameAs(expected);
        }
    }
}
