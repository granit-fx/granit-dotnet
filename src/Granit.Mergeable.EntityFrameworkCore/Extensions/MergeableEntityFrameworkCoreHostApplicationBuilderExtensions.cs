using Granit.Mergeable.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Mergeable.EntityFrameworkCore.Extensions;

/// <summary>
/// Registration helpers for the EF Core merge orchestrator.
/// </summary>
public static class MergeableEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the orchestrator's bookkeeping <c>DbContext</c> (idempotency cache) and the
    /// open-generic <c>EfMergeService&lt;TAggregate&gt;</c> implementation. Each consuming
    /// module then registers its own <c>IMergeableAggregateAdapter&lt;T&gt;</c> + per-module
    /// <c>IReferenceRewriter&lt;T&gt;</c> participants via the helpers in
    /// <c>Granit.Mergeable.Extensions.MergeableServiceCollectionExtensions</c>.
    /// </summary>
    public static IHostApplicationBuilder AddGranitMergeableEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        // Isolated DbContext owning merge_idempotency. Uses the AddGranitDbContext convention
        // (interceptors + naming) so the table follows the framework standards.
        builder.Services.AddGranitDbContext<MergeableDbContext>(configure);

        // Bind orchestrator options (timeout, idempotency retention) from the Mergeable
        // section. Validates DataAnnotations + fail-fast on misconfiguration.
        builder.Services.AddOptions<MergeableOptions>()
            .BindConfiguration(MergeableOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // MAC-key provider — derives a deployment-bound HMAC key from the configured
        // IStringEncryptionService. Singleton: derivation is one-shot and the cached key is
        // safe to share across requests.
        builder.Services.TryAddSingleton<IMergeableSecretProvider, StringEncryptionMergeableSecretProvider>();

        // Open generic — resolved per TAggregate by the consuming module's DI registration.
        // EfMergeService<> is internal but typeof(...) crosses the assembly boundary fine
        // since DI uses reflection to construct it.
        builder.Services.TryAdd(ServiceDescriptor.Scoped(
            typeof(IMergeService<>),
            typeof(EfMergeService<>)));

        return builder;
    }
}
