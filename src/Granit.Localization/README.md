# Granit.Localization

Modular JSON localization engine. IStringLocalizer with embedded resources, inter-module inheritance, and culture fallback.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Localization
```

## Configuration

The module registers default languages out of the box (en, en-GB, fr, fr-CA — `en` is
default). To override with your application's supported languages and custom labels/flag
codes, configure `GranitLocalizationOptions` in your host module's `ConfigureServices`:

```csharp
context.Services.Configure<GranitLocalizationOptions>(options =>
{
    options.Languages.Clear();
    options.Languages.Add(new LanguageInfo("en", "English", "gb", isDefault: true));
    options.Languages.Add(new LanguageInfo("fr", "Français", "fr"));
});
```

`Languages` also drives `SupportedUICultures` for `UseGranitRequestLocalization`. If you
need formatting cultures (number/date/currency) independent from UI languages, populate
`options.FormattingCultures`. JSON resource auto-discovery is enabled by default
(`EnableAutoDiscovery = true`).

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
