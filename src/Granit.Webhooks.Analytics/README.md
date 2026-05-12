# Granit.Webhooks.Analytics

Analytics satellite for Granit.Webhooks — ships 4 MetricDefinitions observing webhook subscriptions and delivery attempts. Optional.

## Usage

```xml
<PackageReference Include="Granit.Webhooks.Analytics" />
```

The satellite's `GranitWebhooksAnalyticsModule` is auto-discovered by the Granit module loader and depends on the host framework module — no extension method to call.

## Architecture

This is a satellite package following the `Granit.{Module}.{Subsystem}` convention. It allows the host framework module to remain free of analytics / dashboards dependencies; consuming hosts opt-in to observability by adding this package.

The runtime targets `Granit.Analytics` / `Granit.Dashboards` (commercial edition); the framework host module functions without this package.
