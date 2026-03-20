using Granit.Caching;
using Granit.Core.Modularity;
using Granit.Localization.Extensions;
using Granit.Localization.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Localization;

/// <summary>
/// Granit module for modular JSON localization.
/// Registers IStringLocalizerFactory and the default Granit resource.
/// </summary>
[DependsOn(typeof(GranitCachingModule))]
public sealed class GranitLocalizationModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitLocalization();

        context.Services.Configure<GranitLocalizationOptions>(options =>
        {
            options.EnableAutoDiscovery = true;

            options.Resources
                .Add<GranitLocalizationResource>("fr")
                .AddJson(
                    typeof(GranitLocalizationResource).Assembly,
                    "Granit.Localization.Localization.Granit");

            options.Languages.Add(new LanguageInfo("fr", "Français (France)", "fr"));
            options.Languages.Add(new LanguageInfo("fr-CA", "Français (Canada)", "ca"));
            options.Languages.Add(new LanguageInfo("en", "English (United States)", "us", isDefault: true));
            options.Languages.Add(new LanguageInfo("en-GB", "English (United Kingdom)", "gb"));
        });
    }
}
