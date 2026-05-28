using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Persistence.MultiTenancy;
using Granit.QueryEngine;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Webhooks.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for enabling EF Core persistence in Granit.Webhooks.
/// </summary>
public static class WebhooksEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Replaces the default InMemory/no-op stores with durable EF Core implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be called after <c>AddGranitWebhooks()</c>. Registers:
    /// </para>
    /// <list type="bullet">
    ///   <item><see cref="EfWebhookSubscriptionStore"/> — replaces <c>InMemoryWebhookSubscriptionStore</c> for both <see cref="IWebhookSubscriptionReader"/> and <see cref="IWebhookSubscriptionWriter"/>.</item>
    ///   <item><see cref="EfWebhookDeliveryStore"/> — replaces <c>NullWebhookDeliveryWriter</c> (enables ISO 27001 audit trail).</item>
    ///   <item><see cref="Internal.WebhooksDbContext"/> — registered via <c>IDbContextFactory</c> for thread-safe usage in Wolverine handlers.</item>
    /// </list>
    /// <para>
    /// <b>Storage mode (ADR-063).</b> Webhooks is a dual-scope module: platform-managed
    /// subscriptions (<c>TenantId == null</c>) and tenant-managed subscriptions
    /// (<c>TenantId == &lt;tenant&gt;</c>) are functionally distinct but share the same
    /// entity shape. <see cref="WebhooksEntityFrameworkCoreOptions.StorageMode"/> selects
    /// the physical layout:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="DualScopeStorageMode.Shared"/> (default) — single host table, row-level
    ///     filter on <c>TenantId</c>. Tables live in <see cref="GranitDbDefaults.HostDbSchema"/>
    ///     (see <see cref="GranitWebhooksDbProperties.DbSchema"/>). Backwards compatible with
    ///     deployments that existed before ADR-063 shipped.
    ///   </item>
    ///   <item>
    ///     <see cref="DualScopeStorageMode.Segregated"/> — host rows in a host-pinned context,
    ///     tenant rows in an isolated context (per-tenant schema or per-tenant database).
    ///     <b>Implementation pending — Phase 2B of Epic #2377.</b> Requested today,
    ///     registration throws <see cref="NotSupportedException"/>.
    ///   </item>
    /// </list>
    /// <para>
    /// <b>Folding into the consuming app's own <see cref="DbContext"/></b> (under
    /// <see cref="DualScopeStorageMode.Shared"/>): call
    /// <see cref="WebhooksModelBuilderExtensions.ConfigureWebhooksModule"/> on a
    /// <b>host-scoped</b> DbContext (registered via <c>AddGranitDbContext&lt;T&gt;</c>) —
    /// never on a tenant-isolated DbContext (<c>AddGranitIsolatedDbContext&lt;T&gt;</c>).
    /// Folding into an isolated DbContext under <c>SchemaPerTenant</c> or
    /// <c>DatabasePerTenant</c> creates the table in each tenant's schema while
    /// <see cref="Internal.WebhooksDbContext"/> still qualifies queries against
    /// <c>host.webhooks_subscriptions</c>, leading to a <c>42P01 relation does not exist</c>
    /// error at the first request. The internal
    /// <c>WebhooksDualScopeIntegrationValidator</c> fails fast at host startup if this
    /// misconfiguration is detected.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">
    /// Configuration callback for the <see cref="WebhooksEntityFrameworkCoreOptions"/>.
    /// </param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="configure"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// When <see cref="WebhooksEntityFrameworkCoreOptions.StorageMode"/> is
    /// <see cref="DualScopeStorageMode.Shared"/> and
    /// <see cref="WebhooksEntityFrameworkCoreOptions.Configure"/> is <c>null</c>, or when
    /// <see cref="DualScopeStorageMode.Segregated"/> is combined with
    /// <see cref="TenantIsolationStrategy.SharedDatabase"/> (rejected by ADR-063).
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// When <see cref="WebhooksEntityFrameworkCoreOptions.StorageMode"/> is
    /// <see cref="DualScopeStorageMode.Segregated"/> — implementation lands in Phase 2B
    /// of Epic #2377.
    /// </exception>
    public static IHostApplicationBuilder AddGranitWebhooksEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<WebhooksEntityFrameworkCoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        WebhooksEntityFrameworkCoreOptions options = new();
        configure(options);

        TenantIsolationStrategy strategy = ResolveTenantIsolationStrategy(builder.Configuration);
        DualScopeValidation.ValidateStorageMode(options.StorageMode, strategy, moduleName: "Webhooks");

        switch (options.StorageMode)
        {
            case DualScopeStorageMode.Shared:
                RegisterSharedMode(builder, options);
                break;

            case DualScopeStorageMode.Segregated:
                throw new NotSupportedException(
                    "WebhooksEntityFrameworkCoreOptions.StorageMode = DualScopeStorageMode.Segregated " +
                    "is not yet implemented. The framework primitive shipped in granit-dotnet #2386; " +
                    "the Webhooks context split lands in Phase 2B of Epic #2377. " +
                    "Set StorageMode to DualScopeStorageMode.Shared (the default) to keep the current behaviour.");

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(configure),
                    options.StorageMode,
                    "Unknown DualScopeStorageMode value.");
        }

        return builder;
    }

    private static void RegisterSharedMode(
        IHostApplicationBuilder builder,
        WebhooksEntityFrameworkCoreOptions options)
    {
        if (options.Configure is null)
        {
            throw new InvalidOperationException(
                "WebhooksEntityFrameworkCoreOptions.Configure must be set when StorageMode is " +
                "DualScopeStorageMode.Shared (the default). Provide an Action<DbContextOptionsBuilder> " +
                "that configures the EF Core provider and connection string for the shared host context.");
        }

        builder.Services.AddGranitDbContext<WebhooksDbContext>(options.Configure);
        builder.Services.AddHostedService<WebhooksDualScopeIntegrationValidator>();

        builder.Services.AddScoped<EfWebhookSubscriptionStore>();
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IWebhookSubscriptionReader>(sp => sp.GetRequiredService<EfWebhookSubscriptionStore>()));
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IWebhookSubscriptionWriter>(sp => sp.GetRequiredService<EfWebhookSubscriptionStore>()));
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IWebhookSigningKeyReader>(sp => sp.GetRequiredService<EfWebhookSubscriptionStore>()));
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IWebhookSigningKeyWriter>(sp => sp.GetRequiredService<EfWebhookSubscriptionStore>()));

        builder.Services.Replace(
            ServiceDescriptor.Scoped<IWebhookDeliveryWriter, EfWebhookDeliveryStore>());
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IWebhookDeliveryReader, EfWebhookDeliveryStore>());

        builder.Services.Replace(
            ServiceDescriptor.Scoped<IWebhookStatsReader, EfWebhookStatsReader>());
        builder.Services.AddScoped<IQueryableSource<WebhookSubscription>, EfWebhookSubscriptionQueryableSource>();
        builder.Services.AddScoped<IQueryableSource<WebhookDeliveryAttempt>, EfWebhookDeliveryAttemptQueryableSource>();
    }

    private static TenantIsolationStrategy ResolveTenantIsolationStrategy(IConfiguration configuration)
    {
        TenantIsolationOptions? bound = configuration
            .GetSection("MultiTenancy:TenantIsolation")
            .Get<TenantIsolationOptions>();

        return bound?.Strategy ?? TenantIsolationStrategy.SharedDatabase;
    }
}
