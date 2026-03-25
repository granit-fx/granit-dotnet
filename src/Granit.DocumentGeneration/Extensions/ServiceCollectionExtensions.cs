using Granit.DocumentGeneration.Diagnostics;
using Granit.DocumentGeneration.Internal;
using Granit.DocumentGeneration.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.DocumentGeneration.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.DocumentGeneration</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the document generation pipeline infrastructure.
    /// </summary>
    /// <remarks>
    /// Registers the following services:
    /// <list type="bullet">
    ///   <item><see cref="IDocumentGenerator"/> (scoped)</item>
    /// </list>
    /// <para>
    /// At least one <see cref="IDocumentRenderer"/> must be registered separately.
    /// Use <see cref="AddDocumentRenderer{TRenderer}"/> after calling this method.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitDocumentGeneration(this IServiceCollection services)
    {
        services.TryAddSingleton<DocumentGenerationMetrics>();
        services.TryAddScoped<IDocumentGenerator, DocumentGenerator>();
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IDocumentRenderer"/> implementation.
    /// </summary>
    /// <typeparam name="TRenderer">The renderer implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDocumentRenderer<TRenderer>(
        this IServiceCollection services)
        where TRenderer : class, IDocumentRenderer =>
        services.AddSingleton<IDocumentRenderer, TRenderer>();
}
