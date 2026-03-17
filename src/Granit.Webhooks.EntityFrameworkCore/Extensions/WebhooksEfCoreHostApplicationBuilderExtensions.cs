using Granit.Persistence.Extensions;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Webhooks.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for enabling EF Core persistence in Granit.Webhooks.
/// </summary>
public static class WebhooksEfCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Replaces the default InMemory/no-op stores with durable EF Core implementations
    /// backed by a PostgreSQL database.
    /// </summary>
    /// <remarks>
    /// Must be called after <c>AddGranitWebhooks()</c>.
    /// Registers:
    /// <list type="bullet">
    ///   <item><see cref="EfWebhookSubscriptionStore"/> — replaces <c>InMemoryWebhookSubscriptionStore</c> for both <see cref="IWebhookSubscriptionReader"/> and <see cref="IWebhookSubscriptionWriter"/>.</item>
    ///   <item><see cref="EfWebhookDeliveryStore"/> — replaces <c>NullWebhookDeliveryWriter</c> (enables ISO 27001 audit trail).</item>
    ///   <item><see cref="Internal.WebhooksDbContext"/> — registered via <c>IDbContextFactory</c> for thread-safe usage in Wolverine handlers.</item>
    /// </list>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitWebhooksEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<WebhooksDbContext>(configure);

        builder.Services.AddScoped<EfWebhookSubscriptionStore>();
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IWebhookSubscriptionReader>(sp => sp.GetRequiredService<EfWebhookSubscriptionStore>()));
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IWebhookSubscriptionWriter>(sp => sp.GetRequiredService<EfWebhookSubscriptionStore>()));

        builder.Services.Replace(
            ServiceDescriptor.Scoped<IWebhookDeliveryWriter, EfWebhookDeliveryStore>());
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IWebhookDeliveryReader, EfWebhookDeliveryStore>());

        return builder;
    }
}
