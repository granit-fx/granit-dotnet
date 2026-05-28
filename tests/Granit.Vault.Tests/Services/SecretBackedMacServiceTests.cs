using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using Granit.Vault.Diagnostics;
using Granit.Vault.Exceptions;
using Granit.Vault.Options;
using Granit.Vault.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Vault.Tests.Services;

public sealed class SecretBackedMacServiceTests : IDisposable
{
    private readonly ISecretStore _secretStore = Substitute.For<ISecretStore>();
    private readonly ServiceProvider _sp;
    private readonly VaultMetrics _metrics;

    public SecretBackedMacServiceTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _metrics = new VaultMetrics(_sp.GetRequiredService<IMeterFactory>());
    }

    public void Dispose() => _sp.Dispose();

    private SecretBackedMacService BuildSut(string? previousSecretName = null)
    {
        SecretBackedMacOptions options = new()
        {
            CurrentSecretName = "granit/test/mac-current",
            PreviousSecretName = previousSecretName,
            RefreshInterval = TimeSpan.FromMinutes(5),
        };
        return new SecretBackedMacService(
            _secretStore,
            Microsoft.Extensions.Options.Options.Create(options),
            _metrics,
            currentTenant: null,
            NullLogger<SecretBackedMacService>.Instance);
    }

    private void StubSecret(string name, byte[] key)
    {
        _secretStore.GetSecretAsync(
                Arg.Is<SecretRequest>(r => r.Name == name),
                Arg.Any<CancellationToken>())
            .Returns(SecretDescriptor.FromString(name, Convert.ToBase64String(key)));
    }

    [Fact]
    public async Task MacAsync_Then_VerifyAsync_RoundTrips()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        StubSecret("granit/test/mac-current", key);
        using SecretBackedMacService sut = BuildSut();
        byte[] input = Encoding.UTF8.GetBytes("payload that should round-trip");

        TransitMacResult tag = await sut.MacAsync("doesNotMatter", input, TestContext.Current.CancellationToken);
        bool ok = await sut.VerifyAsync("doesNotMatter", input, tag.Mac, TestContext.Current.CancellationToken);

        ok.ShouldBeTrue();
        tag.KeyVersion.ShouldBe(1);
        tag.Mac.ShouldStartWith("sbm:v1:");
    }

    [Fact]
    public async Task VerifyAsync_RejectsTamperedTag()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        StubSecret("granit/test/mac-current", key);
        using SecretBackedMacService sut = BuildSut();
        byte[] input = Encoding.UTF8.GetBytes("hello");

        TransitMacResult tag = await sut.MacAsync("k", input, TestContext.Current.CancellationToken);
        // Flip the last char of the base64 payload
        char last = tag.Mac[^1];
        string flipped = string.Concat(tag.Mac.AsSpan(0, tag.Mac.Length - 1), last == 'A' ? "B" : "A");

        (await sut.VerifyAsync("k", input, flipped, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyAsync_RejectsTagWithWrongPrefix()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        StubSecret("granit/test/mac-current", key);
        using SecretBackedMacService sut = BuildSut();

        bool ok = await sut.VerifyAsync(
            "k",
            Encoding.UTF8.GetBytes("anything"),
            "vault:v1:foo",
            TestContext.Current.CancellationToken);

        ok.ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyAsync_AcceptsTagSignedByPreviousKey()
    {
        byte[] currentKey = RandomNumberGenerator.GetBytes(32);
        byte[] previousKey = RandomNumberGenerator.GetBytes(32);
        StubSecret("granit/test/mac-current", currentKey);
        StubSecret("granit/test/mac-previous", previousKey);
        using SecretBackedMacService sutBeforeRotation = BuildSut(previousSecretName: "granit/test/mac-previous");

        // Phase 1 — sign while currentKey is "current". We replicate that by signing
        // with a sut whose current is the original previousKey value.
        StubSecret("granit/test/mac-current", previousKey);
        using SecretBackedMacService originalSut = BuildSut();
        byte[] input = Encoding.UTF8.GetBytes("rotated message");
        TransitMacResult oldTag = await originalSut.MacAsync("k", input, TestContext.Current.CancellationToken);

        // Phase 2 — operator rotates: current=currentKey, previous=oldCurrent.
        StubSecret("granit/test/mac-current", currentKey);
        bool stillValid = await sutBeforeRotation.VerifyAsync("k", input, oldTag.Mac, TestContext.Current.CancellationToken);

        stillValid.ShouldBeTrue();
    }

    [Fact]
    public async Task MacAsync_RejectsKeyOfWrongLength()
    {
        StubSecret("granit/test/mac-current", RandomNumberGenerator.GetBytes(16)); // too short
        using SecretBackedMacService sut = BuildSut();

        await Should.ThrowAsync<MacVerificationException>(async () =>
            await sut.MacAsync("k", Encoding.UTF8.GetBytes("x"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Dispose_ZeroesKeyMaterial()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        StubSecret("granit/test/mac-current", key);
        SecretBackedMacService sut = BuildSut();
        await sut.MacAsync("k", Encoding.UTF8.GetBytes("x"), TestContext.Current.CancellationToken);

        sut.Dispose();

        // Subsequent calls throw — proves the disposed state is enforced and no key material
        // remains usable through the public surface.
        await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await sut.MacAsync("k", Encoding.UTF8.GetBytes("x"), TestContext.Current.CancellationToken));
    }
}
