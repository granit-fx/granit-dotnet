---
title: "ADR-008: OpenTelemetry for Distributed Tracing"
description: "Implement OpenTelemetry browser tracing for end-to-end request correlation between frontend and .NET backend"
sidebar:
  order: 8
  label: "008 - OpenTelemetry"
---

> **Date:** 2026-03-04
> **Authors:** Jean-Francois Meyers
> **Scope:** @granit/tracing, @granit/react-tracing, @granit/logger-otlp

## Context

The platform requires distributed tracing to:

- Follow requests end-to-end (frontend → .NET backend → database)
- Diagnose performance issues
- Correlate frontend logs with backend traces
- Feed an observability backend (Grafana Tempo)

The .NET backend already uses OpenTelemetry (see
[ADR-001](/architecture/adr/001-observability/)). The frontend tracing
must use the same standard for seamless correlation.

## Decision

Use **OpenTelemetry** via the `@granit/tracing` package, which encapsulates:

- `WebTracerProvider` for SDK initialization
- `OTLPTraceExporter` (HTTP) for trace export
- Auto-instrumentations: `fetch`, `XMLHttpRequest`, `document-load`
- `TracingProvider` (React context) for activation in the component tree
- `useTracer` and `useSpan` hooks for custom spans
- `getTraceContext` for non-React integration (e.g. `@granit/logger-otlp`)

The `@granit/logger-otlp` package extends `@granit/logger` to inject trace
IDs into logs, enabling log-to-trace correlation in Grafana.

All OpenTelemetry dependencies are declared as `peerDependencies`:

```json
{
  "peerDependencies": {
    "@opentelemetry/api": "^1.9.0",
    "@opentelemetry/sdk-trace-web": "^2.6.0",
    "@opentelemetry/exporter-trace-otlp-http": "^0.213.0",
    "@opentelemetry/instrumentation-fetch": "^0.213.0",
    "@opentelemetry/instrumentation-xml-http-request": "^0.213.0",
    "@opentelemetry/instrumentation-document-load": "^0.57.0",
    "@opentelemetry/resources": "^2.6.0",
    "@opentelemetry/semantic-conventions": "^1.40.0",
    "@opentelemetry/context-zone": "^2.6.0"
  }
}
```

## Alternatives considered

### Option 1: OpenTelemetry (selected)

- **Advantage**: CNCF standard, vendor-agnostic, same protocol as .NET backend,
  auto-instrumentation for HTTP and page load, self-hostable collector
- **Disadvantage**: 9 peer dependencies, web SDK less mature than server-side

### Option 2: Proprietary solution (Datadog, New Relic)

- **Advantage**: complete SaaS solution (logs + traces + metrics + APM)
- **Disadvantage**: **incompatible with data sovereignty** — US company subject
  to Cloud Act, high cost per host/GB

### Option 3: Custom trace propagation

- **Advantage**: minimal dependencies, full control
- **Disadvantage**: non-standard, no auto-instrumentation, no ecosystem tooling,
  no correlation with backend traces

## Justification

| Criterion | OpenTelemetry | Datadog / New Relic | Custom |
| --------- | ------------- | ------------------- | ------ |
| Sovereignty | Self-hosted collector | No (US) | Self-hosted |
| CNCF standard | Yes | Partial (proprietary agents) | No |
| Backend correlation | Same protocol (.NET OTel) | Proprietary | Manual |
| Auto-instrumentation | fetch, XHR, page load | Full | None |
| Cost | Infrastructure only | Per-host/GB | Development |

## Packages used

| Package | Role |
| ------- | ---- |
| `@opentelemetry/api` | Core tracing API |
| `@opentelemetry/sdk-trace-web` | Browser tracer provider |
| `@opentelemetry/exporter-trace-otlp-http` | OTLP HTTP trace export |
| `@opentelemetry/instrumentation-fetch` | Auto-instrumentation for `fetch()` |
| `@opentelemetry/instrumentation-xml-http-request` | Auto-instrumentation for XHR |
| `@opentelemetry/instrumentation-document-load` | Page load tracing |
| `@opentelemetry/resources` | Resource metadata (service name, version) |
| `@opentelemetry/semantic-conventions` | Standard attribute names |
| `@opentelemetry/context-zone` | Zone.js context propagation |

## Consequences

### Positive

- Standard compliance: same OTLP protocol as the .NET backend, seamless
  end-to-end correlation
- Vendor-agnostic: the observability backend can be changed without modifying
  frontend code
- Automatic correlation: trace IDs propagate automatically between frontend
  and backend
- Auto-instrumentation: HTTP requests (fetch, XHR) and page load are traced
  automatically
- Data sovereignty: the OTLP collector is self-hosted on European infrastructure

### Negative

- Peer dependency count: 9 OpenTelemetry packages add configuration overhead
  in consumer applications
- Web SDK maturity: the OpenTelemetry web SDK is less mature than server-side
  SDKs (Node.js, .NET)
- Performance: instrumentation adds slight overhead to HTTP requests (mitigated
  by graceful degradation when the collector is absent)

## Re-evaluation conditions

This decision should be re-evaluated if:

- The OpenTelemetry web SDK is deprecated in favor of a different approach
- Browser-native tracing APIs emerge (e.g. Performance Observer extensions)
- The number of peer dependencies becomes a significant maintenance burden

## References

- OpenTelemetry JS: <https://opentelemetry.io/docs/languages/js/>
- OpenTelemetry Web SDK: <https://opentelemetry.io/docs/languages/js/getting-started/browser/>
- ADR-001 (.NET Observability): [ADR-001](/architecture/adr/001-observability/)
