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
16K×16K pixels, 32-frame max) when the host starts. Limits are process-global ImageMagick
state, applied by a hosted service — they are not active in non-hosted containers (e.g. a
bare `ServiceCollection` in tests); the per-request guards (`MaxInputBytes`, magic-byte
allowlisting) do not depend on them.

## Configuration

`ImagingMagickNetOptions` binds to the `Imaging:MagickNet` section and is validated at
startup:

```json
{
  "Imaging": {
    "MagickNet": {
      "MaxMemoryBytes": 268435456,
      "MaxWidthPixels": 16384,
      "MaxHeightPixels": 16384,
      "MaxListLength": 32,
      "MaxInputBytes": 52428800
    }
  }
}
```

Code-supplied values win over configuration:

```csharp
builder.AddGranitImagingMagickNet(options => options.MaxInputBytes = 25 * 1024 * 1024);
```

## Dependencies

- `Granit.Imaging`

## Documentation

See the [full documentation](https://granit-fx.dev).
