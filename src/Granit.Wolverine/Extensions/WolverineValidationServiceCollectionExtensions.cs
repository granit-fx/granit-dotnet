using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Attributes;

namespace Granit.Wolverine.Extensions;

/// <summary>
/// Extension methods for auto-discovering FluentValidation validators from
/// assemblies marked with <see cref="WolverineHandlerModuleAttribute"/>.
/// </summary>
public static class WolverineValidationServiceCollectionExtensions
{
    /// <summary>
    /// Scans all loaded assemblies decorated with
    /// <c>[assembly: WolverineHandlerModule]</c> and registers their
    /// <see cref="IValidator{T}"/> implementations as scoped services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method must be called <strong>after</strong> all application assemblies
    /// have been loaded (typically at startup, after module registration).
    /// It uses <see cref="AppDomain.CurrentDomain"/> to enumerate loaded assemblies,
    /// filtering on <see cref="WolverineHandlerModuleAttribute"/>.
    /// </para>
    /// <para>
    /// Modules that contain validators but are <strong>not</strong> Wolverine handler
    /// modules (e.g. a Core module with no handlers) must still register their
    /// validators manually via
    /// <c>ValidationServiceCollectionExtensions.AddGranitValidatorsFromAssemblyContaining{T}</c>.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitValidatorsFromWolverineHandlerModules(
        this IServiceCollection services)
    {
        foreach (Assembly? assembly in AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetCustomAttribute<WolverineHandlerModuleAttribute>() is not null))
        {
            services.AddValidatorsFromAssembly(
                assembly, ServiceLifetime.Scoped, includeInternalTypes: true);
        }

        return services;
    }
}
