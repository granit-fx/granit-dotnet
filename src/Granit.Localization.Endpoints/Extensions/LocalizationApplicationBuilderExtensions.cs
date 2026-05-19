// ---------------------------------------------------------------------------
// LocalizationApplicationBuilderExtensions.cs
// Configures ASP.NET Core request localization from GranitLocalizationOptions.
// ---------------------------------------------------------------------------

using System.Globalization;
using Granit.Localization.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Localization.Endpoints.Extensions;

/// <summary>
/// Extension methods to configure ASP.NET Core request localization from
/// <see cref="GranitLocalizationOptions"/>.
/// </summary>
public static class LocalizationApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the ASP.NET Core <see cref="RequestLocalizationMiddleware"/> configured from
    /// <see cref="GranitLocalizationOptions"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="GranitLocalizationOptions.Languages"/> drives <c>SupportedUICultures</c>
    /// (translation resolution).
    /// </para>
    /// <para>
    /// When <see cref="GranitLocalizationOptions.FormattingCultures"/> is empty (default),
    /// <c>SupportedCultures</c> is set to the same cultures as <c>SupportedUICultures</c>.
    /// When populated, <c>SupportedCultures</c> uses the explicit formatting cultures instead.
    /// </para>
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">
    /// Optional delegate to further customize <see cref="RequestLocalizationOptions"/>
    /// after Granit applies its defaults.
    /// </param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseGranitRequestLocalization(
        this IApplicationBuilder app,
        Action<RequestLocalizationOptions>? configure = null)
    {
        GranitLocalizationOptions granitOptions = app.ApplicationServices
            .GetRequiredService<IOptions<GranitLocalizationOptions>>().Value;

        app.UseRequestLocalization(options =>
        {
            ConfigureFromGranitOptions(options, granitOptions);
            configure?.Invoke(options);
        });

        return app;
    }

    internal static void ConfigureFromGranitOptions(
        RequestLocalizationOptions options,
        GranitLocalizationOptions granitOptions)
    {
        var uiCultures = granitOptions.Languages
            .Select(l => new CultureInfo(l.CultureName))
            .ToList();

        List<CultureInfo> formattingCultures = granitOptions.FormattingCultures.Count > 0
            ? granitOptions.FormattingCultures
            : uiCultures;

        options.SupportedCultures = formattingCultures;
        options.SupportedUICultures = uiCultures;

        LanguageInfo? defaultLanguage = granitOptions.Languages.FirstOrDefault(l => l.IsDefault)
            ?? granitOptions.Languages.FirstOrDefault();

        if (defaultLanguage is not null)
        {
            options.SetDefaultCulture(defaultLanguage.CultureName);
        }
    }
}
