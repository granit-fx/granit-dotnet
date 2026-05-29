using System.Text;
using Granit.Auditing.Domain;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Auditing.EntityFrameworkCore.Internal;

/// <summary>
/// Fail-fast guard for the <c>Shared</c> storage mode: detects when a tenant-isolated
/// <see cref="DbContext"/> outside Granit.Auditing folds the Auditing model via
/// <see cref="Extensions.AuditingModelBuilderExtensions.ConfigureAuditingModule"/>.
/// </summary>
internal sealed partial class AuditingDualScopeIntegrationValidator(
    IEnumerable<IsolatedDbContextMarker> markers,
    IServiceProvider serviceProvider,
    ILogger<AuditingDualScopeIntegrationValidator> logger) : IHostedService
{
    private static readonly HashSet<Type> AuditingOwnedContexts =
    [
        typeof(AuditingTenantDbContext),
    ];

    private static readonly HashSet<Type> AuditingAggregates =
    [
        typeof(AuditEntry),
        typeof(AuditEntityChange),
        typeof(AuditPropertyChange),
    ];

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        List<(Type DbContextType, List<string> Entities)> offenders = [];

        foreach (IsolatedDbContextMarker marker in markers)
        {
            if (AuditingOwnedContexts.Contains(marker.DbContextType))
            {
                continue;
            }

            List<string>? folded = TryFindFoldedAuditingEntities(marker.DbContextType);
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
            "Granit.Auditing is a dual-scope module: its tables live in the host schema " +
            "and tenant isolation is enforced by the MultiTenant row-level query filter. " +
            "ConfigureAuditingModule() must be folded into a host-scoped DbContext " +
            "(registered via AddGranitDbContext<T>), never into a tenant-isolated DbContext " +
            "(registered via AddGranitIsolatedDbContext<T>) — otherwise the migration " +
            "creates the table in the tenant schema while runtime queries target the host " +
            "schema, causing PostgreSQL 42P01 at the first request.");
        sb.AppendLine();
        sb.AppendLine("The following isolated DbContexts incorrectly fold Auditing entities:");
        foreach ((Type type, List<string> entities) in offenders)
        {
            sb.Append("  - ").Append(type.FullName ?? type.Name)
              .Append(" → ").AppendLine(string.Join(", ", entities));
        }
        sb.AppendLine();
        sb.Append("Switch AuditingEntityFrameworkCoreOptions.StorageMode to ");
        sb.Append("DualScopeStorageMode.Segregated if you need physically separate Auditing ");
        sb.Append("tables per tenant.");

        throw new InvalidOperationException(sb.ToString());
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private List<string>? TryFindFoldedAuditingEntities(Type dbContextType)
    {
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
                    if (AuditingAggregates.Contains(entityType.ClrType))
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
            LogValidationSkipped(dbContextType.Name, ex.GetType().Name, ex.Message);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping Auditing dual-scope validation for {ContextType}: no SharedDatabase keyed factory registered.")]
    private partial void LogNoSharedFactory(string contextType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping Auditing dual-scope validation for {ContextType}: model inspection failed with {ExceptionType}: {Message}.")]
    private partial void LogValidationSkipped(string contextType, string exceptionType, string message);
}
