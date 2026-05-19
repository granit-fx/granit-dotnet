using System.Reflection;
using Granit.Authorization.Domain;
using Granit.Authorization.Entities;
using Granit.Authorization.Exports;
using Granit.Authorization.Extensions;
using Granit.Authorization.Queries;
using Granit.Caching;
using Granit.DataExchange.Extensions;
using Granit.Entities.Extensions;
using Granit.Modularity;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Authorization;

/// <summary>
/// Granit module for RBAC permission management.
/// Registers permission definitions, the dynamic ASP.NET Core policy provider,
/// and the caching-aware permission checker.
/// Auto-discovers all <see cref="IPermissionDefinitionProvider"/> implementations
/// across loaded module assemblies.
/// </summary>
[DependsOn(
    typeof(GranitCachingModule))]
public sealed class GranitAuthorizationModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitAuthorization();

        context.Services.AddQueryDefinition<PermissionGrant, PermissionGrantQueryDefinition>();
        context.Services.AddExportDefinition<PermissionGrant, PermissionGrantExportDefinition>();

        context.Services.AddQueryDefinition<RoleMetadata, RoleMetadataQueryDefinition>();
        context.Services.AddExportDefinition<RoleMetadata, RoleMetadataExportDefinition>();

        // Phase 2 EntityDefinitions (ADR-050).
        context.Services.AddEntityDefinition<PermissionGrant, PermissionGrantEntityDefinition>();
        context.Services.AddEntityDefinition<RoleMetadata, RoleMetadataEntityDefinition>();

        foreach (Assembly assembly in context.ModuleAssemblies)
        {
            IEnumerable<Type> providerTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                    && typeof(IPermissionDefinitionProvider).IsAssignableFrom(t));

            foreach (Type providerType in providerTypes)
            {
                context.Services.AddSingleton(typeof(IPermissionDefinitionProvider), providerType);
            }
        }
    }
}
