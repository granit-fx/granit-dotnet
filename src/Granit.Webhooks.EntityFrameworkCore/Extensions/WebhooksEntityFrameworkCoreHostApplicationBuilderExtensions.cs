using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
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
    /// Replaces the default InMemory/no-op stores with durable EF Core implementations
    /// backed by a PostgreSQL database.
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
    /// <b>Integration requirement — host-scoped DbContext only.</b> Webhooks is a
    /// <b>dual-scope</b> module: platform-managed subscriptions (<c>TenantId == null</c>)
    /// coexist with tenant-managed subscriptions (<c>TenantId == &lt;tenant&gt;</c>) in the
    /// same physical table. Tenant isolation is enforced by the row-level <c>MultiTenant</c>
    /// query filter — host admin reads bypass the filter via
    /// <see cref="Internal.EfWebhookSubscriptionQueryableSource"/>. To support this contract
    /// the tables live in <see cref="GranitDbDefaults.HostDbSchema"/>
    /// (see <see cref="GranitWebhooksDbProperties.DbSchema"/>).
    /// </para>
    /// <para>
    /// <b>When folding the model into the consuming app's own <see cref="DbContext"/></b>,
    /// call <see cref="WebhooksModelBuilderExtensions.ConfigureWebhooksModule"/> on a
    /// <b>host-scoped</b> DbContext (one registered via <c>AddGranitDbContext&lt;T&gt;</c>) —
    /// never on a tenant-isolated DbContext (one registered via
    /// <c>AddGranitIsolatedDbContext&lt;T&gt;</c>). Folding into an isolated DbContext under
    /// the <c>SchemaPerTenant</c> or <c>DatabasePerTenant</c> strategy creates the table in
    /// each tenant's schema (e.g. <c>acme.webhooks_subscriptions</c>) while
    /// <see cref="Internal.WebhooksDbContext"/> still qualifies queries against
    /// <c>host.webhooks_subscriptions</c>, leading to a <c>42P01 relation does not exist</c>
    /// error at the first request. The internal
    /// <c>WebhooksDualScopeIntegrationValidator</c> fails fast at host startup if this
    /// misconfiguration is detected.
    /// </para>
    /// <para>
    /// If your deployment genuinely needs <i>physically isolated</i> webhook tables per
    /// tenant (e.g. for <c>DROP SCHEMA</c> tenant lessivage under GDPR), see the dedicated
    /// Epic — this requires a second host DbContext for platform subscriptions and is not
    /// supported by the current <c>AddGranitWebhooksEntityFrameworkCore</c> extension.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitWebhooksEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<WebhooksDbContext>(configure);
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

        return builder;
    }
}
