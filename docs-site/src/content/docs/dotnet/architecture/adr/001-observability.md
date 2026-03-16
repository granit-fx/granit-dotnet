---
title: "ADR-001: Observability Stack — Serilog + OpenTelemetry"
description: "Adoption of Serilog and OpenTelemetry for structured logging, distributed tracing, and metrics with data sovereignty compliance"
sidebar:
  order: 1
  label: "001 - Observability Stack"
---

> **Date:** 2026-02-21
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.Observability)

## Context

The Granit framework provides the `Granit.Observability` module which encapsulates
the configuration of structured logging, distributed tracing and metrics. The
choice of instrumentation libraries determines:

- **ISO 27001 traceability**: structured timestamped logs retained for 3 years
- **Distributed tracing**: request correlation across modules,
  Wolverine messages and HTTP calls
- **Metrics**: performance monitoring and alerting
- **Data sovereignty**: no telemetry data must leave European infrastructure

Observability data is exported via the OTLP protocol to a self-hosted
Grafana stack: **Loki** (logs), **Tempo** (traces), **Mimir** (metrics).

## Decision

- **Logging**: Serilog (`Serilog.AspNetCore` + `Serilog.Sinks.OpenTelemetry`)
- **Tracing & metrics**: OpenTelemetry .NET SDK (7 packages)
- **Export**: OTLP (OpenTelemetry Protocol) to the Grafana stack

## Alternatives considered

### Option 1: Serilog + OpenTelemetry (selected)

- **Logging**: Serilog — structured logging, enrichers (context, tenant, user),
  OTLP sink to unify the pipeline
- **Tracing**: OpenTelemetry — CNCF standard, automatic instrumentation
  (ASP.NET Core, HTTP, EF Core), W3C Trace Context propagation
- **Export**: OTLP to Loki/Tempo/Mimir (self-hosted in Europe)

### Option 2: Microsoft.Extensions.Logging + OpenTelemetry only

- **Advantage**: no third-party dependency for logging
- **Disadvantage**: limited enrichment, no dedicated sink for advanced
  structured formats, less flexible configuration than Serilog
  (namespace filtering, destructuring)

### Option 3: NLog + OpenTelemetry

- **Advantage**: NLog is mature and performant
- **Disadvantage**: less developed OTLP sink ecosystem than Serilog,
  XML configuration (vs fluent API), modern .NET community leans toward Serilog

### Option 4: Application Insights (Azure Monitor)

- **Advantage**: native .NET integration, out-of-the-box dashboard
- **Disadvantage**: **incompatible with data sovereignty** — data hosted on Azure
  (US Cloud Act), variable cost based on ingestion volume, vendor lock-in

### Option 5: Datadog / New Relic

- **Advantage**: complete SaaS solution (logs + traces + metrics + APM)
- **Disadvantage**: **incompatible with data sovereignty** — data outside EU (or EU
  region but US company subject to Cloud Act), high cost per host/GB

## Justification

| Criterion | Serilog + OTel | MEL + OTel | NLog + OTel | App Insights | Datadog |
| --------- | -------------- | ---------- | ----------- | ------------ | ------- |
| Sovereignty | Self-hosted | Self-hosted | Self-hosted | No (Azure) | No (US) |
| CNCF standard | Yes (OTel) | Yes (OTel) | Yes (OTel) | Partial | Partial |
| Log enrichment | Excellent | Basic | Good | Good | Excellent |
| Native OTLP sink | Yes | No | Partial | N/A | N/A |
| .NET community | Very large | Standard | Medium | Large | Medium |
| Cost | Infra only | Infra only | Infra only | Variable | High |
| ISO 27001 compliance | Yes | Yes | Yes | Risk | Risk |

## Packages used

| Package | Role |
| ------- | ---- |
| `Serilog.AspNetCore` | ASP.NET Core integration, request enrichers |
| `Serilog.Sinks.OpenTelemetry` | Log export via OTLP |
| `OpenTelemetry` | Core SDK |
| `OpenTelemetry.Api` | Instrumentation API (ActivitySource, Meter) |
| `OpenTelemetry.Extensions.Hosting` | `IHostBuilder` integration |
| `OpenTelemetry.Instrumentation.AspNetCore` | Automatic HTTP request traces |
| `OpenTelemetry.Instrumentation.Http` | Automatic `HttpClient` call traces |
| `OpenTelemetry.Instrumentation.EntityFrameworkCore` | Automatic EF Core query traces |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | OTLP export to Loki/Tempo/Mimir |

## Consequences

### Positive

- Sovereignty compliance: zero telemetry data leaving European hosting
- CNCF standard: portability to any OTLP-compatible backend
- Full correlation: logs, traces, and metrics via the same TraceId
- Serilog contextual enrichment: tenant, user, module, correlation-id
- Unified Grafana dashboard for the entire platform

### Negative

- Grafana stack maintenance (Loki, Tempo, Mimir) falls on the SRE team
- Initial configuration more complex than a SaaS solution
- Serilog is a third-party dependency (maintenance risk, although very stable)

## Re-evaluation conditions

This decision should be re-evaluated if:

- A European managed observability service emerges (ISO 27001 certified)
- OpenTelemetry .NET SDK reaches feature parity with Serilog for structured logging
- The maintenance burden of the self-hosted Grafana stack becomes disproportionate

## References

- Initial commit: `52f1444` (2026-02-21)
- Serilog: <https://serilog.net/>
- OpenTelemetry .NET: <https://opentelemetry.io/docs/languages/dotnet/>
