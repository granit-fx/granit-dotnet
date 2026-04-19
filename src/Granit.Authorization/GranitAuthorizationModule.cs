using System.Reflection;
using Granit.Authorization.Domain;
using Granit.Authorization.Exports;
using Granit.Authorization.Extensions;
using Granit.Authorization.Queries;
using Granit.Caching;
using Granit.DataExchange.Extensions;
using Granit.Modularity;
using Granit.QueryEngine.Extensions;
using Granit.Users;
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
