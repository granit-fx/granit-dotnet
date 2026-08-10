using System.Globalization;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

namespace Granit.Templating.Scriban.Internal;

/// <summary>
/// Bridges Granit's <see cref="ITemplateResolver"/> chain to Scriban's <see cref="ITemplateLoader"/>
/// so that <c>{{ include 'template_name' }}</c> resolves templates through the standard pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a singleton; creates a <see cref="IServiceScope"/> per load to access
/// scoped resolvers (e.g. <c>StoreTemplateResolver</c> backed by EF Core).
/// Culture is read from <see cref="CultureInfo.CurrentCulture"/> (set by ASP.NET Core
/// request localization middleware).
/// </para>
/// <para>
/// <strong>Async-only:</strong> <see cref="Load"/> throws <see cref="NotSupportedException"/>
/// to enforce async rendering via <c>Template.RenderAsync</c>. The sync path would block threads when
/// resolvers perform I/O (database, network), causing thread starvation under load.
/// </para>
/// </remarks>
internal sealed class GranitTemplateLoader(IServiceProvider serviceProvider) : ITemplateLoader
{
    /// <inheritdoc/>
    /// <remarks>
    /// Returns a culture-qualified cache key so that the same template name in different
    /// cultures produces distinct cache entries in Scriban's <c>CachedTemplates</c> dictionary.
    /// </remarks>
    public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
    {
        string culture = CultureInfo.CurrentCulture.Name;
        return string.IsNullOrEmpty(culture)
            ? templateName
            : $"{templateName}|{culture}";
    }

    /// <summary>
    /// Not supported — Granit enforces async rendering to prevent thread starvation
    /// with scoped DB-backed resolvers.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath) =>
        throw new NotSupportedException(
            "Granit templates require async rendering via Template.RenderAsync. " +
            "Synchronous template loading is not supported to prevent thread starvation.");

    /// <inheritdoc/>
    public async ValueTask<string?> LoadAsync(
        TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        // Parse the culture-qualified cache key back into name + culture
        (string name, string? culture) = ParseTemplatePath(templatePath);

        // Create a scope to access scoped resolvers (StoreTemplateResolver needs DbContext)
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        IEnumerable<ITemplateResolver> resolvers = scope.ServiceProvider
            .GetServices<ITemplateResolver>()
            .OrderByDescending(r => r.Priority);

        // Culture-specific pass
        TemplateKey culturalKey = new(name, culture);
        foreach (ITemplateResolver resolver in resolvers)
        {
            TemplateDescriptor? descriptor = await resolver
                .TryResolveAsync(culturalKey, context.CancellationToken)
                .ConfigureAwait(false);

            if (descriptor is not null)
            {
                return descriptor.Content;
            }
        }

        // Culture-neutral fallback (only if we had a culture to begin with)
        if (culture is not null)
        {
            TemplateKey neutralKey = new(name);
            foreach (ITemplateResolver resolver in resolvers)
            {
                TemplateDescriptor? descriptor = await resolver
                    .TryResolveAsync(neutralKey, context.CancellationToken)
                    .ConfigureAwait(false);

                if (descriptor is not null)
                {
                    return descriptor.Content;
                }
            }
        }

        return null;
    }

    private static (string Name, string? Culture) ParseTemplatePath(string templatePath)
    {
        int separator = templatePath.LastIndexOf('|');
        if (separator < 0)
        {
            return (templatePath, null);
        }

        return (templatePath[..separator], templatePath[(separator + 1)..]);
    }
}
