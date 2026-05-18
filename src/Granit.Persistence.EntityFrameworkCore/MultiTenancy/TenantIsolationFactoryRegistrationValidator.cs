using System.Text;
using Microsoft.Extensions.Options;

namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Fail-fast validator that ensures every <see cref="IsolatedDbContextMarker"/> has a
/// registered keyed factory for the active <see cref="TenantIsolationOptions.Strategy"/>.
/// </summary>
/// <remarks>
/// <para>
/// Runs during <c>host.StartAsync()</c> via the <c>ValidateOnStartHostedService</c>
/// registered by <c>ValidateOnStart()</c> on <see cref="TenantIsolationOptions"/> —
/// before any request is served, before any Wolverine handler, before any seed
/// contributor. Reads only the metadata of the registered
/// <see cref="IsolatedDbContextMarker"/> singletons — it MUST NOT resolve any keyed
/// <see cref="Microsoft.EntityFrameworkCore.IDbContextFactory{TContext}"/> or trigger
/// DbContext resolution. This guarantees the validator stays cheap and side-effect free.
/// </para>
/// <para>
/// Without this validator a misconfigured host would only fail on the first
/// <c>CreateDbContextAsync</c> call — typically deep inside a request handler — with a
/// stack pointing at the consumer rather than the misconfigured registration.
/// </para>
/// </remarks>
internal sealed class TenantIsolationFactoryRegistrationValidator(
    IEnumerable<IsolatedDbContextMarker> markers)
    : IValidateOptions<TenantIsolationOptions>
{
    private readonly IReadOnlyCollection<IsolatedDbContextMarker> _markers = markers.ToArray();

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, TenantIsolationOptions options)
    {
        TenantIsolationStrategy strategy = options.Strategy;

        // SharedDatabase is always registered by AddGranitIsolatedDbContext — short-circuit.
        if (strategy == TenantIsolationStrategy.SharedDatabase)
        {
            return ValidateOptionsResult.Success;
        }

        var missing = _markers
            .Where(m => !m.RegisteredStrategies.Contains(strategy))
            .ToList();

        if (missing.Count == 0)
        {
            return ValidateOptionsResult.Success;
        }

        string delegateName = strategy switch
        {
            TenantIsolationStrategy.DatabasePerTenant => "configureDatabasePerTenant",
            TenantIsolationStrategy.SchemaPerTenant => "configureSchemaPerTenant",
            _ => "configure" + strategy,
        };

        StringBuilder sb = new();
        sb.Append("TenantIsolation:Strategy='").Append(strategy)
          .Append("' but no ").Append(delegateName)
          .AppendLine(" delegate was passed for the following DbContexts:");

        foreach (Type dbContextType in missing.Select(m => m.DbContextType))
        {
            sb.Append("  - ").AppendLine(dbContextType.FullName ?? dbContextType.Name);
        }

        sb.Append("Either pass the delegate in AddGranit{X}EntityFrameworkCore(...) ")
          .Append("or change TenantIsolation:Strategy in appsettings.");

        return ValidateOptionsResult.Fail(sb.ToString());
    }
}
