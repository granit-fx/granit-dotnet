# Granit.Imaging.MagickNet

Magick.NET implementation of `Granit.Imaging`. Provides `IImageProcessor` with native
support for JPEG, PNG, WebP, AVIF, GIF, BMP and TIFF formats. Licensed under Apache 2.0.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Imaging.MagickNet
```

## Integration

Declare the dependency on your host module — no explicit extension call is needed; the
module's `ConfigureServices` calls `AddGranitImagingMagickNet()` via module discovery:

```csharp
[DependsOn(typeof(GranitImagingMagickNetModule))]
public sealed class YourHostModule : GranitModule;
```

This registers `IImageProcessor` and applies secure resource limits (256 MB memory,
16K×16K pixels, 32-frame max). To customize `ImagingMagickNetOptions` (`MaxMemoryBytes`,
`MaxWidthPixels`, `MaxHeightPixels`, `MaxListLength`), call
`AddGranitImagingMagickNet(configure)` directly in your own `ConfigureServices`.

## Dependencies

- `Granit.Imaging`

## Documentation

See the [full documentation](https://granit-fx.dev).
