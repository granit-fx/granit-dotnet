# Granit.Imaging

Image processing abstraction layer for Granit applications. Provides `IImageProcessor`
and `IImagePipeline` — a fluent API for resize, crop, compress, convert, watermark
and metadata stripping operations.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Imaging
```

This package contains interfaces only. Install an implementation package such as
`Granit.Imaging.MagickNet` for concrete image processing.

## Integration

`GranitImagingModule` is an anchor module with no `ConfigureServices` — wiring comes
from the implementation package. To use imaging in your Granit host, declare a
dependency on the provider module (not on this base module) in your host module class:

```csharp
[DependsOn(typeof(GranitImagingMagickNetModule))]
public sealed class YourHostModule : GranitModule;
```

The module system discovers and initializes the dependent modules through the module
graph, registering `IImageProcessor`.

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
