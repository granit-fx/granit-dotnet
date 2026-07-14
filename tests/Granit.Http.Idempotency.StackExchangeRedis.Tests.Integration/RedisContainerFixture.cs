using Testcontainers.Redis;
using Xunit;

namespace Granit.Http.Idempotency.StackExchangeRedis.Tests.Integration;

/// <summary>
/// Shared fixture — starts a single Redis container once per test class.
/// </summary>
public sealed class RedisContainerFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7-alpine")
        .Build();

    /// <summary>Redis connection string (e.g. <c>"localhost:32768"</c>).</summary>
    public string ConnectionString => _container.GetConnectionString();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
