---
title: Operations
description: Deployment, observability, and production configuration for Granit applications
sidebar:
  order: 0
---

This section covers the operational aspects of deploying and running Granit applications
on European sovereign infrastructure.

## Audience

- **SRE**: observability configuration, alerting, incident response
- **DevOps engineers**: CI/CD pipelines, Kubernetes, Helm
- **Platform engineers**: infrastructure sizing, compliance, capacity planning

## Guides

| Guide | Description |
| --- | --- |
| [Deployment](./deployment/) | Kubernetes deployment, Docker, health probes, scaling |
| [Configuration](./configuration/) | Vault secrets, environment variables, appsettings layering |
| [Observability](./observability/) | LGTM stack, Serilog, OpenTelemetry, Grafana dashboards |
| [CI/CD](./ci-cd/) | GitHub Actions pipeline, build, test, pack, publish |
| [Production checklist](./production-checklist/) | Go-live verification for security, GDPR, ISO 27001 |

## Sovereign infrastructure

All Granit applications handling sensitive data **must** be hosted on European
infrastructure compliant with ISO 27001:

| Component | Technology | Constraint |
| --- | --- | --- |
| Compute | Managed Kubernetes (EU region) | Data residency in EU |
| Database | PostgreSQL (managed or self-hosted) | Encrypted at rest |
| Cache | Redis (managed or self-hosted) | Password-protected via Vault |
| Secrets | HashiCorp Vault (self-hosted, Raft storage) | No SaaS secret managers |
| Observability | LGTM stack (Loki, Grafana, Tempo, Mimir) | Self-hosted, EU only |
| Object storage | S3-compatible (MinIO or EU provider) | Encrypted, tenant-isolated |

:::caution[Data sovereignty]
Telemetry, logs, and traces contain personally identifiable information (tenant IDs,
user IDs, correlation data). All observability data must remain within European
infrastructure. US-based cloud services are not permitted for data subject to
GDPR and ISO 27001 compliance.
:::

## Packages referenced in this section

| Package | Role |
| --- | --- |
| `Granit.Diagnostics` | Kubernetes health check endpoints (liveness, readiness, startup) |
| `Granit.Observability` | Serilog + OpenTelemetry OTLP export to LGTM stack |
| `Granit.Vault` | HashiCorp Vault integration (dynamic credentials, Transit encryption) |
| `Granit.Http.Cors` | CORS policy configuration |
| `Granit.Http.ExceptionHandling` | RFC 7807 Problem Details error responses |
| `Granit.Wolverine.Postgresql` | Wolverine messaging with PostgreSQL transport |
