using System.Text;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// Fail-fast guard that detects when a tenant-isolated <see cref="DbContext"/> folds the
/// Webhooks model via <see cref="Extensions.WebhooksModelBuilderExtensions.ConfigureWebhooksModule"/>.
/// </summary>
/// <remarks>
/// <para>
/// Webhooks is a dual-scope module: tables live in the <i>host</i> schema and tenant
/// isolation is enforced via the <c>MultiTenant</c> row-level query filter. Folding
/// <c>ConfigureWebhooksModule()</c> into an isolated DbContext under <c>SchemaPerTenant</c>
/// or <c>DatabasePerTenant</c> creates the table in the wrong place — runtime queries
/// then fail with PostgreSQL 42P01 (<i>relation does not exist</i>).
/// </para>
/// <para>
/// Runs once at <c>host.StartAsync()</c>. For each <see cref="IsolatedDbContextMarker"/>,
/// resolves the always-registered <see cref="TenantIsolationStrategy.SharedDatabase"/>
/// keyed factory (cheap — no real DB I/O, only model compilation), inspects the model's
/// entity types, and throws if any Webhooks aggregate is configured.
/// </para>
/// </remarks>
internal sealed partial class WebhooksDualScopeIntegrationValidator(
    IEnumerable<IsolatedDbContextMarker> markers,
    IServiceProvider serviceProvider,
    ILogger<WebhooksDualScopeIntegrationValidator> logger) : IHostedService
{
    private static readonly HashSet<Type> WebhooksAggregates =
    [
        typeof(WebhookSubscription),
        typeof(WebhookSigningKey),
        typeof(WebhookDeliveryAttempt),
    ];

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        List<(Type DbContextType, List<string> Entities)> offenders = [];

        foreach (IsolatedDbContextMarker marker in markers)
        {
            List<string>? folded = TryFindFoldedWebhooksEntities(marker.DbContextType);
            if (folded is { Count: > 0 })
            {
                offenders.Add((marker.DbContextType, folded));
            }
        }

        if (offenders.Count == 0)
        {
            return Task.CompletedTask;
        }

        StringBuilder sb = new();
        sb.AppendLine(
            "Webhooks is a dual-scope module: its tables live in the host schema and tenant " +
            "isolation is enforced by the MultiTenant row-level query filter. " +
            "ConfigureWebhooksModule() must be folded into a host-scoped DbContext " +
            "(registered via AddGranitDbContext<T>), never into a tenant-isolated DbContext " +
            "(registered via AddGranitIsolatedDbContext<T>) — otherwise the migration creates " +
            "the table in the tenant schema while runtime queries target the host schema, " +
            "causing PostgreSQL 42P01 at the first request.");
        sb.AppendLine();
        sb.AppendLine("The following isolated DbContexts incorrectly fold Webhooks entities:");
        foreach ((Type type, List<string> entities) in offenders)
        {
            sb.Append("  - ").Append(type.FullName ?? type.Name)
              .Append(" → ").AppendLine(string.Join(", ", entities));
        }
        sb.AppendLine();
        sb.Append("Move ConfigureWebhooksModule() to a host-scoped DbContext, or open the ");
        sb.Append("fully-isolated Webhooks Epic if you need physically separate tables per tenant.");

        throw new InvalidOperationException(sb.ToString());
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private List<string>? TryFindFoldedWebhooksEntities(Type dbContextType)
    {
        // The keyed IDbContextFactory<T> registered by AddGranitIsolatedDbContext is scoped
        // (EF Core default). Resolving it through the root provider trips CallSiteValidator
        // when the host enables ValidateScopes (default in dev). Open a per-call scope so
        // the resolution is legal regardless of the consumer's ServiceProviderOptions.
        using IServiceScope scope = serviceProvider.CreateScope();

        Type factoryType = typeof(IDbContextFactory<>).MakeGenericType(dbContextType);
        object? factory = scope.ServiceProvider.GetKeyedService(factoryType, TenantIsolationStrategy.SharedDatabase);
        if (factory is null)
        {
            LogNoSharedFactory(dbContextType.Name);
            return null;
        }

        try
        {
            var context = (DbContext)factoryType
                .GetMethod(nameof(IDbContextFactory<DbContext>.CreateDbContext))!
                .Invoke(factory, parameters: null)!;

            try
            {
                List<string> matches = [];
                foreach (IEntityType entityType in context.Model.GetEntityTypes())
                {
                    if (WebhooksAggregates.Contains(entityType.ClrType))
                    {
                        matches.Add(entityType.ClrType.Name);
                    }
                }
                return matches;
            }
            finally
            {
                context.Dispose();
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            // Best-effort: a DbContext that throws at model-compile time is already broken
            // elsewhere — let the real validator handle it. Don't mask unrelated startup
            // failures behind a Webhooks-specific message.
            LogValidationSkipped(dbContextType.Name, ex.GetType().Name, ex.Message);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping Webhooks dual-scope validation for {ContextType}: no SharedDatabase keyed factory registered.")]
    private partial void LogNoSharedFactory(string contextType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping Webhooks dual-scope validation for {ContextType}: model inspection failed with {ExceptionType}: {Message}.")]
    private partial void LogValidationSkipped(string contextType, string exceptionType, string message);
}
