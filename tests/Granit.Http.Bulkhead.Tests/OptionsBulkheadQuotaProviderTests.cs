using Granit.Http.Bulkhead.Internal;
using Granit.Http.Bulkhead.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Bulkhead.Tests;

public sealed class OptionsBulkheadQuotaProviderTests
{
    [Fact]
    public async Task GetPermitLimitAsync_KnownPolicy_ReturnsPermitLimit()
    {
        GranitBulkheadOptions options = new()
        {
            Policies = new Dictionary<string, BulkheadPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new() { PermitLimit = 42 },
            },
        };

        TestOptionsMonitor monitor = CreateMonitor(options);
        OptionsBulkheadQuotaProvider sut = new(monitor);

        int? result = await sut.GetPermitLimitAsync("api", TestContext.Current.CancellationToken);

        result.ShouldBe(42);
    }

    [Fact]
    public async Task GetPermitLimitAsync_UnknownPolicy_ReturnsNull()
    {
        GranitBulkheadOptions options = new();
        TestOptionsMonitor monitor = CreateMonitor(options);
        OptionsBulkheadQuotaProvider sut = new(monitor);

        int? result = await sut.GetPermitLimitAsync("nonexistent", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetPermitLimitAsync_CaseInsensitiveMatch()
    {
        GranitBulkheadOptions options = new()
        {
            Policies = new Dictionary<string, BulkheadPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["Api"] = new() { PermitLimit = 15 },
            },
        };

        TestOptionsMonitor monitor = CreateMonitor(options);
        OptionsBulkheadQuotaProvider sut = new(monitor);

        int? result = await sut.GetPermitLimitAsync("api", TestContext.Current.CancellationToken);

        result.ShouldBe(15);
    }

    private static TestOptionsMonitor CreateMonitor(GranitBulkheadOptions options) =>
        new(options);

    private sealed class TestOptionsMonitor(GranitBulkheadOptions value) : IOptionsMonitor<GranitBulkheadOptions>
    {
        public GranitBulkheadOptions CurrentValue => value;

        public GranitBulkheadOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<GranitBulkheadOptions, string?> listener) => null;
    }
}
