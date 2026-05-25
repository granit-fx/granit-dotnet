using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Html.AngleSharp.Extensions;

/// <summary>
/// Extension methods for registering the AngleSharp-backed
/// <see cref="IHtmlToPlainTextConverter"/> implementation.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AngleSharpHtmlToPlainTextConverter"/> as the default
    /// <see cref="IHtmlToPlainTextConverter"/>. Uses
    /// <see cref="AngleSharpConfiguration.BuildForTrustedTemplates"/>. Hosts dealing with
    /// untrusted content should replace the registration with a converter constructed via
    /// <see cref="AngleSharpConfiguration.BuildForUntrustedContent"/>.
    /// </summary>
    public static IServiceCollection AddGranitHtmlAngleSharp(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IHtmlToPlainTextConverter, AngleSharpHtmlToPlainTextConverter>();
        return services;
    }
}
