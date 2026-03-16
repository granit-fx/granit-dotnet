---
title: "ADR-013: Magick.NET — Image Processing"
description: "Selection of Magick.NET (ImageMagick wrapper) for cross-platform image processing with Apache-2.0 licensing"
sidebar:
  order: 13
  label: "013 - Magick.NET"
---

> **Date:** 2026-02-28
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.Imaging.MagickNet)

## Context

The `Granit.Imaging` module provides an image processing pipeline for the
platform: resizing, format conversion, compression, watermarking, and
metadata extraction.

Use cases include: profile photos, scanned documents, medical images
(excluding DICOM), and compliance watermarks.

Requirements:

- **Formats**: JPEG, PNG, WebP, TIFF, BMP, GIF (minimum)
- **Transformations**: resize, crop, rotate, watermark, format conversion
- **Cross-platform**: Linux (production K8s) and Windows (development)
- **License**: compatible with commercial use

## Decision

**Magick.NET** (.NET wrapper for ImageMagick, Q8 variant) for image processing.

## Alternatives considered

### Option 1: Magick.NET-Q8-AnyCPU (selected)

- **License**: Apache-2.0
- **Advantage**: 200+ supported formats, complete transformations (resize, crop,
  watermark, compression), cross-platform (native wrapper), Q8 variant
  (8 bits/channel — optimized memory), .NET fluent API, very mature
  (ImageMagick: 25+ years)

### Option 2: ImageSharp (SixLabors)

- **License**: **Six Labors Split License** (v3+) — commercial use requires
  a **paid license** (enterprise $2,500/year)
- **Advantage**: 100% managed .NET (no native dependency), modern API,
  excellent performance, composable pipeline
- **Disadvantage**: license change in 2023 (v3), recurring cost for
  production features, fewer formats than Magick.NET

### Option 3: SkiaSharp

- **License**: MIT
- **Advantage**: Skia engine (Google), performant for 2D rendering, cross-platform
- **Disadvantage**: rendering/drawing-oriented (not image processing), low-level API
  for common transformations (resize, watermark), native Skia dependency
  (~10 MB), not all formats supported (no TIFF, partial WebP)

### Option 4: System.Drawing.Common

- **License**: MIT (Microsoft)
- **Advantage**: native .NET Framework, familiar API
- **Disadvantage**: **Windows-only** since .NET 6 (no Linux support — blocking
  for K8s production), deprecated by Microsoft, known memory leaks,
  not thread-safe

## Justification

| Criterion | Magick.NET | ImageSharp | SkiaSharp | System.Drawing |
| --------- | ---------- | ---------- | --------- | -------------- |
| License | Apache-2.0 | Commercial (v3+) | MIT | MIT |
| Cost | Free | $2,500/year | Free | Free |
| Formats | 200+ | ~30 | ~15 | ~10 |
| Cross-platform | Yes (native) | Yes (managed) | Yes (native) | Windows only |
| Watermark | Native | Native | Manual | Manual |
| Maturity | 25+ years (IM) | 7+ years | 10+ years | 20+ years (deprecated) |
| Thread-safe | Yes | Yes | Partial | No |

## Consequences

### Positive

- 200+ supported formats, covering all current and future use cases
- Apache-2.0: no license cost (ImageSharp would be ~$2,500/year)
- Cross-platform: works on Linux (K8s production) and Windows (dev)
- Q8 variant: optimized memory (8 bits/channel sufficient for web)
- Native watermark for compliance watermarks

### Negative

- Native ImageMagick dependency (libMagickWand) — handled by the AnyCPU
  package but may cause issues in some Docker environments
  (requires system libraries)
- Less modern API than ImageSharp (C API wrapper)
- Larger package size than managed alternatives (~15 MB)
- Historical ImageMagick vulnerabilities (ImageTragick 2016) — mitigated by
  built-in security policies and regular updates
