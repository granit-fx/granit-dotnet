using System.Text;
using Granit.Identity.Domain;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// Fail-fast guard for the <c>Shared</c> storage mode: detects when a tenant-isolated
/// <see cref="DbContext"/> outside Granit.Identity folds the User model via
/// <see cref="Extensions.IdentityModelBuilderExtensions.ConfigureGranitIdentityModule"/>.
/// </summary>
/// <remarks>
/// <para>
/// Under <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared"/> the User
/// table lives in the host schema and tenant isolation is enforced via the
/// <c>MultiTenant</c> row-level query filter. Folding
/// <c>ConfigureGranitIdentityModule()</c> into a consumer's isolated DbContext under
/// <c>SchemaPerTenant</c> or <c>DatabasePerTenant</c> creates the table in the wrong place
/// — runtime queries then fail with PostgreSQL 42P01.
/// </para>
/// <para>
/// Under <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Segregated"/> this
/// validator is not registered: Identity itself owns an isolated
/// <c>IdentityTenantDbContext</c>, so the heuristic "User entity in an isolated context =
/// misconfiguration" no longer holds.
/// </para>
/// </remarks>
internal sealed partial class IdentityDualScopeIntegrationValidator(
    IEnumerable<IsolatedDbContextMarker> markers,
    IServiceProvider serviceProvider,
    ILogger<IdentityDualScopeIntegrationValidator> logger) : IHostedService
{
    private static readonly HashSet<Type> IdentityOwnedContexts =
    [
        typeof(IdentityTenantDbContext),
    ];

    private static readonly HashSet<Type> IdentityAggregates =
    [
        typeof(User),
    ];

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        List<(Type DbContextType, List<string> Entities)> offenders = [];

        foreach (IsolatedDbContextMarker marker in markers)
        {
            if (IdentityOwnedContexts.Contains(marker.DbContextType))
            {
                continue;
            }

            List<string>? folded = TryFindFoldedIdentityEntities(marker.DbContextType);
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
            "Granit.Identity is a dual-scope module: the User table lives in the host " +
            "schema and tenant isolation is enforced by the MultiTenant row-level query " +
            "filter. ConfigureGranitIdentityModule() must be folded into a host-scoped " +
            "DbContext (registered via AddGranitDbContext<T>), never into a tenant-isolated " +
            "DbContext (registered via AddGranitIsolatedDbContext<T>) — otherwise the " +
            "migration creates the table in the tenant schema while runtime queries target " +
            "the host schema, causing PostgreSQL 42P01 at the first request.");
        sb.AppendLine();
        sb.AppendLine("The following isolated DbContexts incorrectly fold Identity entities:");
        foreach ((Type type, List<string> entities) in offenders)
        {
            sb.Append("  - ").Append(type.FullName ?? type.Name)
              .Append(" → ").AppendLine(string.Join(", ", entities));
        }
        sb.AppendLine();
        sb.Append("Switch IdentityEntityFrameworkCoreOptions.StorageMode to ");
        sb.Append("DualScopeStorageMode.Segregated if you need physically separate User ");
        sb.Append("tables per tenant.");

        throw new InvalidOperationException(sb.ToString());
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private List<string>? TryFindFoldedIdentityEntities(Type dbContextType)
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
                    if (IdentityAggregates.Contains(entityType.ClrType))
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping Identity dual-scope validation for {ContextType}: no SharedDatabase keyed factory registered.")]
    private partial void LogNoSharedFactory(string contextType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping Identity dual-scope validation for {ContextType}: model inspection failed with {ExceptionType}: {Message}.")]
    private partial void LogValidationSkipped(string contextType, string exceptionType, string message);
}
