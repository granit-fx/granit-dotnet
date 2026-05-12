# Granit.BlobStorage.Analytics

Analytics satellite for Granit.BlobStorage — ships 3 MetricDefinitions observing the BlobDescriptor population (valid count, total size, orphan count). Optional package; the framework module functions without it.

## Usage

```xml
<PackageReference Include="Granit.BlobStorage.Analytics" />
```

The satellite's `GranitBlobStorageAnalyticsModule` is auto-discovered by the Granit module loader and depends on the host framework module — no extension method to call.

## Architecture

This is a satellite package following the `Granit.{Module}.{Subsystem}` convention. It allows the host framework module to remain free of analytics / dashboards dependencies; consuming hosts opt-in to observability by adding this package.

The runtime targets `Granit.Analytics` / `Granit.Dashboards` (commercial edition); the framework host module functions without this package.
