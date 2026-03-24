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

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
