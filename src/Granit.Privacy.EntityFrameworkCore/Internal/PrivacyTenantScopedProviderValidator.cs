using System.Text;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Privacy.DataExport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Privacy.EntityFrameworkCore.Internal;

/// <summary>
/// Fail-fast guard that detects <see cref="IPrivacyDataProvider"/> implementations
/// depending on a tenant-isolated <see cref="DbContext"/> while
/// <c>Granit.MultiTenancy</c> is not wired.
/// </summary>
/// <remarks>
/// <para>
/// Companion to <c>WebhooksDualScopeIntegrationValidator</c> (#2376). The privacy
/// equivalent of the dual-scope misconfig is subtler: there is no wrong-schema fold,
/// but if a provider's constructor takes a tenant-isolated DbContext and
/// <see cref="ICurrentTenant"/> resolves to <see cref="NullTenantContext"/>, the
/// DbContext silently falls back to the <c>SharedDatabase</c> keyed factory
/// (<see cref="Granit.Persistence.EntityFrameworkCore.Extensions.PersistenceTenantExtensions"/>
/// scoped-fallback) and queries the host schema regardless of which tenant the
/// envelope addresses — leading to PostgreSQL 42P01 on the first request.
/// </para>
/// <para>
/// The framework saga (<c>PersonalDataExportSaga.Start</c>) opens the tenant scope
/// from the envelope payload for the HasData probe path; the export-handler path
/// relies on <c>TenantContextBehavior</c> restoring the X-Tenant-Id header. Both
/// require <see cref="ICurrentTenant"/> to be a real implementation, not the null
/// fallback. This validator throws if a tenant-isolated dependency is present but
/// multi-tenancy isn't registered, and logs the detected dependencies otherwise as
/// discoverability for operators auditing privacy data flow.
/// </para>
/// </remarks>
internal sealed partial class PrivacyTenantScopedProviderValidator(
    IDataProviderRegistry providerRegistry,
    IEnumerable<IsolatedDbContextMarker> isolatedMarkers,
    ICurrentTenant currentTenant,
    ILogger<PrivacyTenantScopedProviderValidator> logger) : IHostedService
{
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        HashSet<Type> isolatedDbContextTypes = [.. isolatedMarkers.Select(m => m.DbContextType)];
        if (isolatedDbContextTypes.Count == 0)
        {
            return Task.CompletedTask;
        }

        List<(string ProviderName, Type ProviderType, IReadOnlyList<Type> IsolatedDbContexts)> dependencies = [];

        foreach (ProviderRegistration registration in providerRegistry.GetAllRegistrations())
        {
            if (registration.ProviderType is null)
            {
                continue;
            }

            IReadOnlyList<Type> deps = FindIsolatedDbContextDependencies(
                registration.ProviderType, isolatedDbContextTypes);
            if (deps.Count > 0)
            {
                dependencies.Add((registration.ProviderName, registration.ProviderType, deps));
            }
        }

        if (dependencies.Count == 0)
        {
            return Task.CompletedTask;
        }

        bool multiTenancyMissing = currentTenant is NullTenantContext;
        if (multiTenancyMissing)
        {
            StringBuilder sb = new();
            sb.AppendLine(
                "Privacy data providers depend on tenant-isolated DbContexts but " +
                "Granit.MultiTenancy is not registered (ICurrentTenant resolves to " +
                "NullTenantContext). Tenant-isolated DbContexts silently fall back to " +
                "the SharedDatabase keyed factory in that configuration, so per-tenant " +
                "tables would be queried against the host schema (PostgreSQL 42P01).");
            sb.AppendLine();
            sb.AppendLine("Offending providers:");
            foreach ((string name, Type type, IReadOnlyList<Type> deps) in dependencies)
            {
                sb.Append("  - ").Append(name)
                  .Append(" (").Append(type.FullName ?? type.Name).Append(')')
                  .Append(" → ").AppendLine(string.Join(", ", deps.Select(d => d.Name)));
            }
            sb.AppendLine();
            sb.Append("Call AddGranitMultiTenancy() on the host, or move the provider to a ");
            sb.Append("host-scoped DbContext (registered via AddGranitDbContext).");

            throw new InvalidOperationException(sb.ToString());
        }

        foreach ((string name, Type type, IReadOnlyList<Type> deps) in dependencies)
        {
            LogTenantScopedProvider(name, type.Name, string.Join(", ", deps.Select(d => d.Name)));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static IReadOnlyList<Type> FindIsolatedDbContextDependencies(
        Type providerType, HashSet<Type> isolatedDbContextTypes)
    {
        // Inspect every public constructor's parameter list. Most providers expose a single
        // primary ctor, but we don't assume — any ctor reachable by DI is a potential bind
        // site, and a false negative here would defeat the validator's purpose.
        HashSet<Type> matches = [.. providerType.GetConstructors()
            .SelectMany(ctor => ctor.GetParameters())
            .Select(param => param.ParameterType)
            .Where(isolatedDbContextTypes.Contains)];
        return [.. matches];
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Privacy provider {ProviderName} ({ProviderType}) depends on tenant-isolated DbContext(s) {IsolatedDbContexts}. The framework saga and TenantContextBehavior anchor the tenant scope automatically; custom orchestrators must open ICurrentTenant.Change(context.TenantId) before resolving the provider.")]
    private partial void LogTenantScopedProvider(string providerName, string providerType, string isolatedDbContexts);
}
