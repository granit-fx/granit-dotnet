using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Options;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Identity.Local.AspNetIdentity.Tests;

/// <summary>
/// Boundary tests for the WebAuthn rewrite in <c>AspNetPasskeyService</c>.
/// </summary>
/// <remarks>
/// <para>
/// The previous test suite (33 tests) verified the broken stub behaviour where
/// the service stored random bytes at registration and accepted any assertion
/// whose credential id matched a row. Those tests were intentionally deleted
/// because their assertions encoded the audit findings — keeping them would
/// lock the bugs in.
/// </para>
/// <para>
/// The cryptographic correctness of the new implementation is the responsibility
/// of <c>Fido2NetLib</c>'s own test suite. Hosts validate the integration with
/// real authenticators in a browser-driven integration test
/// (see the <c>passkey-end-to-end</c> follow-up issue).
/// </para>
/// <para>
/// What this file covers right now is the round-trip behaviour of
/// <see cref="PasskeyChallengeStore"/> — the boundary between the cryptographic
/// layer and the receiver's persistence. The challenge store is what protects
/// against ceremony replay (every challenge is consumed on first read) and stale
/// ceremonies (TTL bounded by <see cref="GranitPasskeyOptions.ChallengeLifetime"/>).
/// </para>
/// </remarks>
public sealed class PasskeyChallengeStoreTests
{
    private readonly IFusionCache _cache;

    public PasskeyChallengeStoreTests()
    {
        ServiceCollection services = [];
        services.AddFusionCache();
        _cache = services.BuildServiceProvider().GetRequiredService<IFusionCache>();
    }

    private PasskeyChallengeStore CreateStore(TimeSpan? lifetime = null) =>
        new(_cache, Microsoft.Extensions.Options.Options.Create(new GranitPasskeyOptions
        {
            ServerDomain = "example.com",
            AllowedOrigins = ["https://example.com"],
            ChallengeLifetime = lifetime ?? TimeSpan.FromMinutes(5),
        }));

    [Fact]
    public async Task SaveAndConsumeRegistration_RoundTripsPayload()
    {
        PasskeyChallengeStore store = CreateStore();
        await store.SaveRegistrationAsync("user-1", "options-json", TestContext.Current.CancellationToken);

        string? consumed = await store.ConsumeRegistrationAsync("user-1", TestContext.Current.CancellationToken);

        consumed.ShouldBe("options-json");
    }

    [Fact]
    public async Task ConsumeRegistration_AfterFirstRead_ReturnsNull()
    {
        // Replay protection: per WebAuthn §13.1 a challenge must never be accepted
        // twice. The store burns the entry on first consumption.
        PasskeyChallengeStore store = CreateStore();
        await store.SaveRegistrationAsync("user-1", "options-json", TestContext.Current.CancellationToken);

        await store.ConsumeRegistrationAsync("user-1", TestContext.Current.CancellationToken);
        string? second = await store.ConsumeRegistrationAsync("user-1", TestContext.Current.CancellationToken);

        second.ShouldBeNull();
    }

    [Fact]
    public async Task ConsumeRegistration_WithoutSave_ReturnsNull()
    {
        PasskeyChallengeStore store = CreateStore();

        string? consumed = await store.ConsumeRegistrationAsync("user-1", TestContext.Current.CancellationToken);

        consumed.ShouldBeNull();
    }

    [Fact]
    public async Task SaveAndConsumeAssertion_RoundTripsPayload()
    {
        PasskeyChallengeStore store = CreateStore();
        await store.SaveAssertionAsync("ceremony-1", "assertion-options", TestContext.Current.CancellationToken);

        string? consumed = await store.ConsumeAssertionAsync("ceremony-1", TestContext.Current.CancellationToken);

        consumed.ShouldBe("assertion-options");
    }

    [Fact]
    public async Task ConsumeAssertion_AfterFirstRead_ReturnsNull()
    {
        PasskeyChallengeStore store = CreateStore();
        await store.SaveAssertionAsync("ceremony-1", "options", TestContext.Current.CancellationToken);

        await store.ConsumeAssertionAsync("ceremony-1", TestContext.Current.CancellationToken);
        string? second = await store.ConsumeAssertionAsync("ceremony-1", TestContext.Current.CancellationToken);

        second.ShouldBeNull();
    }

    [Fact]
    public async Task RegistrationAndAssertion_KeyspacesAreIsolated()
    {
        // A registration challenge for user "alice" must not satisfy an assertion
        // ceremony with key "alice", and vice-versa — they live in separate
        // namespaces inside the cache.
        PasskeyChallengeStore store = CreateStore();
        await store.SaveRegistrationAsync("alice", "reg-options", TestContext.Current.CancellationToken);

        string? leaked = await store.ConsumeAssertionAsync("alice", TestContext.Current.CancellationToken);

        leaked.ShouldBeNull();
    }
}
