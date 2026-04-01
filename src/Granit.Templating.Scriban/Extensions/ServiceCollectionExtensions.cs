using Granit.Templating.Extensions;
using Granit.Templating.Pipeline;
using Granit.Templating.Scriban.GlobalContexts;
using Granit.Templating.Scriban.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Templating.Scriban.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Templating.Scriban</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Scriban template engine and its built-in global contexts.
    /// </summary>
    /// <remarks>
    /// Registers the following services:
    /// <list type="bullet">
    ///   <item><see cref="ITemplateEngine"/> → <see cref="ScribanTemplateEngine"/> (singleton)</item>
    ///   <item><see cref="GranitTemplateLoader"/> — enables <c>{{ include 'template_name' }}</c> via resolver chain</item>
    ///   <item><c>now.*</c> global context (singleton, uses <c>IClock</c>)</item>
    ///   <item><c>context.*</c> global context (singleton, soft-depends on <c>ICurrentTenant</c>)</item>
    ///   <item><c>app.*</c> global context (singleton, bound to <c>Granit:Templating:App</c>)</item>
    /// </list>
    /// <para>
    /// Also calls <see cref="ServiceCollectionExtensions.AddGranitTemplating"/> to ensure the
    /// core pipeline is registered.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitTemplatingWithScriban(
        this IServiceCollection services)
    {
        services.AddGranitTemplating();

        services.TryAddSingleton<GranitTemplateLoader>();
        services.TryAddSingleton<ITemplateEngine>(sp =>
            new ScribanTemplateEngine(sp.GetService<GranitTemplateLoader>()));

        services.AddTemplateGlobalContext<NowGlobalContext>();
        services.AddTemplateGlobalContext<ExecutionContextGlobalContext>();
        services.AddTemplateGlobalContext<AppGlobalContext>();

        services.AddOptions<AppGlobalContextOptions>()
            .BindConfiguration(AppGlobalContextOptions.SectionName);

        return services;
    }
}
