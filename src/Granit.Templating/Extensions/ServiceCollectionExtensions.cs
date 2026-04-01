using System.Reflection;
using Granit.Diagnostics;
using Granit.Templating.Diagnostics;
using Granit.Templating.Enrichment;
using Granit.Templating.GlobalContext;
using Granit.Templating.Internal;
using Granit.Templating.Layouts;
using Granit.Templating.Layouts.Internal;
using Granit.Templating.Pipeline;
using Granit.Templating.Resolvers;
using Granit.Templating.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Templating.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Templating</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core templating pipeline infrastructure.
    /// </summary>
    /// <remarks>
    /// Registers the following services:
    /// <list type="bullet">
    ///   <item><see cref="ITextTemplateRenderer"/> (scoped) — text rendering facade.</item>
    /// </list>
    /// <para>
    /// At least one <see cref="ITemplateEngine"/> and one <see cref="ITemplateResolver"/>
    /// must be registered separately. Use <c>Granit.Templating.Scriban</c> for the engine.
    /// </para>
    /// <para>
    /// Use <see cref="AddEmbeddedTemplates"/> to register a fallback resolver for
    /// embedded assembly resources.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitTemplating(this IServiceCollection services)
    {
        // Diagnostics
        GranitActivitySourceRegistry.Register(TemplatingActivitySource.Name);
        services.TryAddSingleton<TemplatingMetrics>();

        services.TryAddScoped<ITextTemplateRenderer, TextTemplateRenderer>();
        services.TryAddSingleton<ITemplateTransitionHook, NullTemplateTransitionHook>();

        // Layout registry — built from all AddTemplateLayout() registrations
        services.TryAddSingleton<ILayoutRegistry>(sp =>
            new LayoutRegistry(sp.GetServices<LayoutRegistration>()));

        return services;
    }

    /// <summary>
    /// Registers an <see cref="ITemplateResolver"/> that resolves templates from
    /// embedded assembly resources.
    /// </summary>
    /// <remarks>
    /// Can be called multiple times with different assemblies. All registered assemblies
    /// are searched in registration order.
    /// <para>
    /// Resource naming convention: <c>{AssemblyName}.Templates.{TemplateName}.html</c>
    /// (culture-neutral) or <c>{AssemblyName}.Templates.{TemplateName}.{culture}.html</c>.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">Assembly containing embedded template resources.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEmbeddedTemplates(
        this IServiceCollection services, Assembly assembly)
    {
        services.AddSingleton<ITemplateResolver>(
            new EmbeddedTemplateResolver([assembly]));
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="ITemplateGlobalContext"/> implementation.
    /// </summary>
    /// <typeparam name="TContext">The context implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTemplateGlobalContext<TContext>(
        this IServiceCollection services)
        where TContext : class, ITemplateGlobalContext =>
        services.AddSingleton<ITemplateGlobalContext, TContext>();

    /// <summary>
    /// Registers an <see cref="ITemplateDataEnricher{TData}"/> that enriches the data model
    /// before rendering.
    /// </summary>
    /// <typeparam name="TData">The data model type.</typeparam>
    /// <typeparam name="TEnricher">The enricher implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTemplateDataEnricher<TData, TEnricher>(
        this IServiceCollection services)
        where TData : notnull
        where TEnricher : class, ITemplateDataEnricher<TData> =>
        services.AddTransient<ITemplateDataEnricher<TData>, TEnricher>();

    /// <summary>
    /// Registers a layout mapping: templates matching <paramref name="templatePattern"/>
    /// are automatically wrapped in the <paramref name="layoutTemplateName"/> layout
    /// during rendering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Patterns support exact names (<c>"Billing.Invoice"</c>) and prefix wildcards
    /// (<c>"Billing.*"</c>). Exact matches always take precedence over prefix matches.
    /// </para>
    /// <para>
    /// The layout template is resolved through the standard <see cref="ITemplateResolver"/>
    /// chain — embedded resources or database store. A tenant can override a layout by
    /// publishing their own version to the store.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="templatePattern">
    /// Exact template name (<c>"Billing.Invoice"</c>) or prefix wildcard
    /// (<c>"Billing.*"</c>) matching all templates in that namespace.
    /// </param>
    /// <param name="layoutTemplateName">
    /// Logical name of the layout template (e.g. <c>"Layout.Email"</c>).
    /// </param>
    /// <param name="priority">
    /// Resolution priority when multiple prefix patterns match. Higher wins. Default: 0.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTemplateLayout(
        this IServiceCollection services,
        string templatePattern,
        string layoutTemplateName,
        int priority = 0)
    {
        services.AddSingleton(new LayoutRegistration(templatePattern, layoutTemplateName, priority));
        return services;
    }
}
