# Granit.Identity.Federated.Analytics

Analytics satellite for Granit.Identity.Federated — ships 2 MetricDefinitions observing the cached federated-identity pool (total + enabled counts). Optional.

## Usage

```xml
<PackageReference Include="Granit.Identity.Federated.Analytics" />
```

The satellite's `GranitIdentityFederatedAnalyticsModule` is auto-discovered by the Granit module loader and depends on the host framework module — no extension method to call.

## Architecture

This is a satellite package following the `Granit.{Module}.{Subsystem}` convention. It allows the host framework module to remain free of analytics / dashboards dependencies; consuming hosts opt-in to observability by adding this package.

The runtime targets `Granit.Analytics` / `Granit.Dashboards` (commercial edition); the framework host module functions without this package.
