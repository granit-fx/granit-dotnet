using System.Reflection;
using Granit.DataProtection;
using Granit.Http.ExceptionHandling.Internal;
using Granit.Http.ExceptionHandling.Options;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Http.ExceptionHandling.Extensions;

/// <summary>
/// Extensions for registering Granit exception handling services.
/// </summary>
public static class ExceptionHandlingServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Granit centralized exception handling pipeline:
    /// <list type="bullet">
    ///   <item>RFC 7807 Problem Details support (<c>AddProblemDetails</c>)</item>
    ///   <item><see cref="GranitExceptionHandler"/> as the active <see cref="IExceptionHandler"/></item>
    ///   <item><see cref="DefaultExceptionStatusCodeMapper"/> as the fallback mapper</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Call <c>app.UseGranitExceptionHandling()</c> in the middleware pipeline
    /// <b>before</b> routing and authorization to ensure all exceptions are caught.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Optional configuration delegate for <see cref="ExceptionHandlingOptions"/>. Runs after the
    /// <c>Http:ExceptionHandling</c> configuration section is bound and can override individual values.
    /// </param>
    public static IServiceCollection AddGranitExceptionHandling(
        this IServiceCollection services,
        Action<ExceptionHandlingOptions>? configure = null)
    {
        services
            .AddOptions<ExceptionHandlingOptions>()
            .BindConfiguration(ExceptionHandlingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddProblemDetails();
        services.AddExceptionHandler<GranitExceptionHandler>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IExceptionStatusCodeMapper, DefaultExceptionStatusCodeMapper>());

        // SensitivePropertyRegistry is a cross-cutting primitive (MCP, audit,
        // and now ProblemDetails sanitization). TryAdd guards against
        // duplicate registration when the consumer also uses Granit.Mcp.
        services.TryAddSingleton(_ =>
        {
            IEnumerable<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name?.StartsWith("Granit", StringComparison.Ordinal) == true);
            return new SensitivePropertyRegistry(assemblies);
        });
        services.TryAddSingleton<ValidationErrorsSanitizer>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }
}
