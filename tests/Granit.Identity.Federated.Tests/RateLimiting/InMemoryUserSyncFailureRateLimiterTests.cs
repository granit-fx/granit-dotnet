using Granit.Identity.Federated.Options;
using Granit.Identity.Federated.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Tests.RateLimiting;

public sealed class InMemoryUserSyncFailureRateLimiterTests
{
    private static InMemoryUserSyncFailureRateLimiter CreateLimiter(
        FakeTimeProvider timeProvider,
        int coolOffMinutes = 60)
    {
        var options = new IdentityFederatedNotificationOptions { SyncFailureCoolOffMinutes = coolOffMinutes };
        IOptionsMonitor<IdentityFederatedNotificationOptions> monitor =
            new StaticOptionsMonitor(options);
        return new InMemoryUserSyncFailureRateLimiter(timeProvider, monitor);
    }

    [Fact]
    public void TryAcquire_FirstCall_AllowsEmission()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        InMemoryUserSyncFailureRateLimiter limiter = CreateLimiter(time);

        limiter.TryAcquire("user-1", "Keycloak").ShouldBeTrue();
    }

    [Fact]
    public void TryAcquire_SecondCallWithinWindow_Suppresses()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        InMemoryUserSyncFailureRateLimiter limiter = CreateLimiter(time);

        limiter.TryAcquire("user-1", "Keycloak").ShouldBeTrue();
        limiter.TryAcquire("user-1", "Keycloak").ShouldBeFalse();
    }

    [Fact]
    public void TryAcquire_AfterWindow_AllowsAgain()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        InMemoryUserSyncFailureRateLimiter limiter = CreateLimiter(time, coolOffMinutes: 60);

        limiter.TryAcquire("user-1", "Keycloak").ShouldBeTrue();
        time.Advance(TimeSpan.FromMinutes(61));
        limiter.TryAcquire("user-1", "Keycloak").ShouldBeTrue();
    }

    [Fact]
    public void TryAcquire_DifferentProvider_AllowsIndependently()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        InMemoryUserSyncFailureRateLimiter limiter = CreateLimiter(time);

        limiter.TryAcquire("user-1", "Keycloak").ShouldBeTrue();
        limiter.TryAcquire("user-1", "EntraId").ShouldBeTrue();
    }

    [Fact]
    public void TryAcquire_DifferentUser_AllowsIndependently()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        InMemoryUserSyncFailureRateLimiter limiter = CreateLimiter(time);

        limiter.TryAcquire("user-1", "Keycloak").ShouldBeTrue();
        limiter.TryAcquire("user-2", "Keycloak").ShouldBeTrue();
    }

    [Fact]
    public void TryAcquire_NullOrWhiteSpaceUserId_Throws()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        InMemoryUserSyncFailureRateLimiter limiter = CreateLimiter(time);

        Should.Throw<ArgumentException>(() => limiter.TryAcquire("", "Keycloak"));
        Should.Throw<ArgumentException>(() => limiter.TryAcquire("   ", "Keycloak"));
    }

    private sealed class StaticOptionsMonitor(IdentityFederatedNotificationOptions value)
        : IOptionsMonitor<IdentityFederatedNotificationOptions>
    {
        public IdentityFederatedNotificationOptions CurrentValue { get; } = value;
        public IdentityFederatedNotificationOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<IdentityFederatedNotificationOptions, string?> listener) => null;
    }
}
