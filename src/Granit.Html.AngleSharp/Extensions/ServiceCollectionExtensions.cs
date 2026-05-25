using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Html.AngleSharp.Extensions;

/// <summary>
/// Extension methods for registering the AngleSharp-backed
/// <see cref="IHtmlToPlainTextConverter"/> implementations.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the two posture profiles of <see cref="IHtmlToPlainTextConverter"/> as
    /// keyed singletons:
    /// <list type="bullet">
    ///   <item><see cref="HtmlConverterKeys.Trusted"/> →
    ///   <see cref="AngleSharpConfiguration.BuildForTrustedTemplates"/> (Notifications.Email,
    ///   host-rendered templates).</item>
    ///   <item><see cref="HtmlConverterKeys.Untrusted"/> →
    ///   <see cref="AngleSharpConfiguration.BuildForUntrustedContent"/> (TextExtraction,
    ///   indexing, anything dealing with externally-sourced HTML).</item>
    /// </list>
    /// Consumers pick the profile at injection time with
    /// <c>[FromKeyedServices(HtmlConverterKeys.Trusted | Untrusted)]</c>.
    /// </summary>
    public static IServiceCollection AddGranitHtmlAngleSharp(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddKeyedSingleton<IHtmlToPlainTextConverter>(
            HtmlConverterKeys.Trusted,
            (_, _) => new AngleSharpHtmlToPlainTextConverter(
                AngleSharpConfiguration.BuildForTrustedTemplates()));

        services.TryAddKeyedSingleton<IHtmlToPlainTextConverter>(
            HtmlConverterKeys.Untrusted,
            (_, _) => new AngleSharpHtmlToPlainTextConverter(
                AngleSharpConfiguration.BuildForUntrustedContent()));

        return services;
    }
}
