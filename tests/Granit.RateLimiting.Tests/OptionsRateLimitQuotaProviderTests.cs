using Granit.RateLimiting.Internal;
using Granit.RateLimiting.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Tests;

public sealed class OptionsRateLimitQuotaProviderTests
{
    [Fact]
    public async Task GetPermitLimitAsync_PolicyExists_ReturnsPermitLimit()
    {
        GranitRateLimitingOptions options = new()
        {
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new() { PermitLimit = 200 },
            },
        };

        TestOptionsMonitor monitor = CreateMonitor(options);
        OptionsRateLimitQuotaProvider provider = new(monitor);

        int? result = await provider.GetPermitLimitAsync("api", TestContext.Current.CancellationToken);

        result.ShouldBe(200);
    }

    [Fact]
    public async Task GetPermitLimitAsync_PolicyDoesNotExist_ReturnsNull()
    {
        GranitRateLimitingOptions options = new()
        {
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase),
        };

        TestOptionsMonitor monitor = CreateMonitor(options);
        OptionsRateLimitQuotaProvider provider = new(monitor);

        int? result = await provider.GetPermitLimitAsync("nonexistent", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetPermitLimitAsync_CaseInsensitiveLookup_ReturnsPermitLimit()
    {
        GranitRateLimitingOptions options = new()
        {
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["Api"] = new() { PermitLimit = 300 },
            },
        };

        TestOptionsMonitor monitor = CreateMonitor(options);
        OptionsRateLimitQuotaProvider provider = new(monitor);

        int? result = await provider.GetPermitLimitAsync("api", TestContext.Current.CancellationToken);

        result.ShouldBe(300);
    }

    private static TestOptionsMonitor CreateMonitor(GranitRateLimitingOptions options) => new(options);

    private sealed class TestOptionsMonitor(GranitRateLimitingOptions options) : IOptionsMonitor<GranitRateLimitingOptions>
    {
        public GranitRateLimitingOptions CurrentValue => options;

        public GranitRateLimitingOptions Get(string? name) => options;

        public IDisposable? OnChange(Action<GranitRateLimitingOptions, string?> listener) => null;
    }
}
