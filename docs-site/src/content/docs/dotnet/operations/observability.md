---
title: Observability
description: Production observability with Serilog, OpenTelemetry, and the Grafana LGTM stack
sidebar:
  order: 3
---

This guide covers the production observability setup for Granit applications:
structured logging with Serilog, distributed tracing and metrics with OpenTelemetry,
and visualization with the Grafana LGTM stack (Loki, Grafana, Tempo, Mimir).

## Architecture

Granit exports all three observability signals via OTLP to a self-hosted
Grafana stack:

| Signal | Library | Collector destination | Storage |
| --- | --- | --- | --- |
| Logs | Serilog 9+ (OTLP sink) | OpenTelemetry Collector | Loki |
| Traces | OpenTelemetry .NET 1.11+ | OpenTelemetry Collector | Tempo |
| Metrics | OpenTelemetry .NET 1.11+ | OpenTelemetry Collector | Mimir |

All telemetry flows through the OpenTelemetry Collector, which routes signals
to the appropriate backend. The entire stack is self-hosted on European
infrastructure -- no telemetry leaves the EU.

## Configuration (Granit.Observability)

A single call to `AddGranitObservability()` configures Serilog and OpenTelemetry:

```csharp
// Program.cs
builder.AddGranitObservability();
```

This registers:

- Serilog with structured logging, console output, and OTLP export
- OpenTelemetry tracing with ASP.NET Core, HttpClient, and EF Core instrumentation
- OpenTelemetry metrics with ASP.NET Core and HttpClient instrumentation
- Automatic enrichment of all log entries with service metadata

### Configuration options

```json
{
  "Observability": {
    "ServiceName": "my-backend",
    "ServiceVersion": "1.2.0",
    "ServiceNamespace": "my-company",
    "Environment": "production",
    "OtlpEndpoint": "http://otel-collector.monitoring:4317",
    "EnableTracing": true,
    "EnableMetrics": true
  }
}
```

| Property | Description | Default |
| --- | --- | --- |
| `ServiceName` | Service identifier in all telemetry signals | `"unknown-service"` |
| `ServiceVersion` | Service version | `"0.0.0"` |
| `ServiceNamespace` | Logical grouping of services | `"my-company"` |
| `Environment` | Deployment environment (`production`, `staging`, `development`) | `"development"` |
| `OtlpEndpoint` | OpenTelemetry Collector gRPC endpoint | `"http://localhost:4317"` |
| `EnableTracing` | Enable trace export via OTLP | `true` |
| `EnableMetrics` | Enable metrics export via OTLP | `true` |

## Structured logging (Loki)

### Automatic enrichment

Every log entry is automatically enriched with the following properties:

| Property | Source | Description |
| --- | --- | --- |
| `ServiceName` | `ObservabilityOptions` | Service identifier |
| `ServiceVersion` | `ObservabilityOptions` | Deployed version |
| `Environment` | `ObservabilityOptions` | Deployment environment |
| `TenantId` | `ICurrentTenant` | Active tenant (if multi-tenancy is configured) |
| `UserId` | `ICurrentUserService` | Authenticated user |
| `TraceId` | `Activity.Current` | OpenTelemetry trace identifier |
| `SpanId` | `Activity.Current` | OpenTelemetry span identifier |
| `MachineName` | System | Kubernetes pod name |

:::note[ISO 27001 audit trail]
The `UserId` and `TenantId` enrichment is mandatory for ISO 27001 compliance.
Every data access must be attributable to a specific user and tenant. These
fields are added automatically -- no manual instrumentation required.
:::

### LogQL queries

Useful queries for production troubleshooting in Grafana:

```logql
# Errors in the last 24 hours for a service
{service_name="my-backend"} | json | Level = "Error"

# Slow requests (> 500ms)
{service_name="my-backend"} | json | RequestDuration > 500

# Activity for a specific tenant
{service_name="my-backend"} | json | TenantId = "tenant-123"

# Audit trail: user access log (ISO 27001)
{service_name="my-backend"} | json | UserId = "john.doe" | Level = "Error"

# Correlate logs with a specific trace
{service_name="my-backend"} | json | TraceId = "abc123def456"
```

### Source-generated logging

Granit requires `[LoggerMessage]` source-generated logging throughout -- never
use string interpolation in log calls:

```csharp
public static partial class LogMessages
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Processing order {OrderId} for tenant {TenantId}")]
    public static partial void ProcessingOrder(this ILogger logger, Guid orderId, Guid tenantId);
}
```

This eliminates boxing allocations and enables compile-time validation of log message templates.

## Distributed tracing (Tempo)

### Automatic instrumentation

`AddGranitObservability()` instruments the following automatically:

- **ASP.NET Core**: incoming HTTP requests (with exception recording)
- **HttpClient**: outgoing HTTP requests
- **EF Core**: SQL queries
- **Wolverine**: message handlers (via `TraceContextBehavior` -- context propagation across async boundaries)
- **Redis**: cache operations (when StackExchange.Redis instrumentation is added)

Health check endpoints (`/health/*`) are excluded from tracing to reduce noise.

### Custom activity sources

Granit modules register their own `ActivitySource` instances via `GranitActivitySourceRegistry`.
These are automatically picked up by the OpenTelemetry tracer configuration -- no manual
`AddSource()` calls needed for Granit modules.

### Log-trace correlation

In Grafana, configure a **data source correlation** between Loki and Tempo.
Clicking a `TraceId` in a log entry opens the corresponding trace in Tempo.
This provides end-to-end request visibility across services.

## Metrics (Mimir)

### Exposed metrics

| Metric | Type | Description |
| --- | --- | --- |
| `http_server_request_duration_seconds` | Histogram | HTTP request duration |
| `http_server_active_requests` | UpDownCounter | In-flight HTTP requests |
| `db_client_operation_duration_seconds` | Histogram | Database operation duration |
| `dotnet_gc_collections_total` | Counter | .NET GC collection count |
| `dotnet_process_memory_bytes` | Gauge | Process memory usage |

### Recommended Grafana dashboards

Build these dashboards for comprehensive production visibility:

1. **Service overview**: request rate, error rate (4xx/5xx), p50/p95/p99 latency
2. **Database**: SQL query duration, connection pool utilization, slow queries
3. **Cache**: hit ratio, Redis latency, evictions
4. **Wolverine**: messages processed/s, error rate, queue depth, dead letter count
5. **Infrastructure**: CPU, memory, GC pressure, thread count

## Alerting

### Recommended alert rules

| Alert | Condition | Severity |
| --- | --- | --- |
| High HTTP error rate | `rate(http_5xx) / rate(http_total) > 0.05` over 5 min | Critical |
| High P99 latency | `p99(http_duration) > 2s` over 5 min | Warning |
| Vault lease renewal failure | Log `"Vault lease renewal failed"` | Critical |
| DB connection pool saturated | `db_pool_active / db_pool_max > 0.9` | Warning |
| Wolverine dead letter queue | `wolverine_dead_letter_count > 0` | Warning |

### Notification channels

| Severity | Channel | Response |
| --- | --- | --- |
| Critical | PagerDuty / OpsGenie | On-call SRE paged immediately |
| Warning | Slack `#ops-alerts` | Investigated within business hours |
| Info | Weekly email digest | Reviewed in operations meeting |

## Data retention

| Signal | Hot retention | Cold retention | ISO 27001 requirement |
| --- | --- | --- | --- |
| Logs | 90 days | 3 years | 3 years minimum (audit trail) |
| Traces | 30 days | -- | Not required |
| Metrics | 1 year | -- | Not required |

:::caution[Log retention compliance]
ISO 27001 requires audit trail logs (data access, authentication events) to be
retained for a minimum of 3 years. Configure Loki retention rules accordingly.
Use cold storage (object storage) for logs beyond the 90-day hot window.
:::

## OpenTelemetry Collector configuration

A minimal Collector configuration for routing Granit signals:

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317

exporters:
  loki:
    endpoint: http://loki:3100/loki/api/v1/push
  otlp/tempo:
    endpoint: http://tempo:4317
    tls:
      insecure: true
  prometheusremotewrite:
    endpoint: http://mimir:9009/api/v1/push

service:
  pipelines:
    logs:
      receivers: [otlp]
      exporters: [loki]
    traces:
      receivers: [otlp]
      exporters: [otlp/tempo]
    metrics:
      receivers: [otlp]
      exporters: [prometheusremotewrite]
```
