using Granit.AI.StackExchangeRedis.Extensions;
using Granit.AI.StackExchangeRedis.Options;
using Granit.Modularity;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Granit.AI.StackExchangeRedis;

/// <summary>
/// Granit module that swaps the in-memory <c>IAICallRateLimiter</c> registered by
/// <c>Granit.AI</c> for the distributed <see cref="Internal.RedisAICallRateLimiter"/>,
/// so the per-tenant LLM call ceiling is enforced once across every replica.
/// </summary>
/// <remarks>
/// <para>
/// Auto-wires only when an <see cref="IConnectionMultiplexer"/> is already registered
/// (typically by <c>Granit.Caching.StackExchangeRedis</c>) — the package is otherwise
/// inert, so a host that pulls it transitively without Redis keeps the safe in-memory
/// default. Set <c>AI:RateLimiting:Redis:Enabled = false</c> to opt out explicitly.
/// </para>
/// <para>
/// If the connection multiplexer is registered by a module that configures <em>after</em>
/// this one (DI registration order is not guaranteed across unrelated modules), call
/// <c>services.AddGranitAIRedisRateLimiter()</c> explicitly instead.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitAIModule))]
public sealed class GranitAIStackExchangeRedisModule : GranitModule
{
    /// <inheritdoc/>
    public override bool IsEnabled(ServiceConfigurationContext context)
    {
        AIRateLimitingRedisOptions options = context.Configuration
            .GetSection(AIRateLimitingRedisOptions.SectionName)
            .Get<AIRateLimitingRedisOptions>() ?? new AIRateLimitingRedisOptions();

        return options.Enabled;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Only take over when Redis is actually wired. Absent a connection multiplexer
        // the in-memory limiter from Granit.AI.Extraction stays in effect — never break
        // a host that referenced this package without configuring Redis.
        bool redisRegistered = context.Services
            .Any(d => d.ServiceType == typeof(IConnectionMultiplexer));

        if (redisRegistered)
        {
            context.Services.AddGranitAIRedisRateLimiter();
        }
    }
}
