using System.Threading.Channels;
using Granit.Auditing.Domain;
using Granit.Auditing.Endpoints.Internal;
using Granit.Auditing.Messages;
using Granit.Auditing.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Auditing.Endpoints.Tests.Internal;

public sealed class AuditingChannelHealthCheckTests
{
    private static AuditingBatch CreateBatch() => new(
        Timestamp: DateTimeOffset.UtcNow,
        UserId: "user-1",
        UserName: "Test User",
        Category: AuditCategory.DataMutation,
        IpAddress: null,
        UserAgent: null,
        TenantId: null,
        CorrelationId: null,
        EntityChanges: []);

    private static async Task<HealthCheckResult> CheckAsync(int capacity, int fill, AuditPersistenceMode mode)
    {
        var channel = Channel.CreateBounded<AuditingBatch>(capacity);
        for (int i = 0; i < fill; i++)
        {
            await channel.Writer.WriteAsync(CreateBatch(), TestContext.Current.CancellationToken);
        }

        AuditingOptions opts = new() { PersistenceMode = mode, ChannelCapacity = capacity };
        AuditingChannelHealthCheck check = new(channel, MsOptions.Create(opts));

        return await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Healthy_InStrictMode_EvenWhenChannelFull()
    {
        HealthCheckResult result = await CheckAsync(capacity: 10, fill: 10, mode: AuditPersistenceMode.Strict);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Healthy_WhenBelowDegradedThreshold()
    {
        HealthCheckResult result = await CheckAsync(capacity: 10, fill: 5, mode: AuditPersistenceMode.Async);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Degraded_WhenAtOrAboveEightyPercent()
    {
        HealthCheckResult result = await CheckAsync(capacity: 10, fill: 8, mode: AuditPersistenceMode.Async);

        result.Status.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public async Task Unhealthy_WhenSaturated()
    {
        HealthCheckResult result = await CheckAsync(capacity: 10, fill: 10, mode: AuditPersistenceMode.Async);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }
}
