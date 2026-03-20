using System.Reflection;
using Granit.Authorization.Abstractions;
using Granit.Authorization.Extensions;
using Granit.Caching;
using Granit.Core.Modularity;
using Granit.Security;
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
    typeof(GranitSecurityModule),
    typeof(GranitCachingModule))]
public sealed class GranitAuthorizationModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitAuthorization();

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
