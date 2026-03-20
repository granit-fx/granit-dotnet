---
title: "Deployment \u2014 Kubernetes, Helm & Docker"
description: Deploy Granit applications on Kubernetes with Docker multi-stage builds, liveness/readiness/startup health probes, Helm values, resource sizing, graceful shutdown, and Alpine-based minimal images.
sidebar:
  order: 1
  label: Deployment
---

This guide covers deploying Granit applications on Kubernetes, including Docker
image build, health check configuration, resource sizing, and graceful shutdown.

## Docker image

Granit applications use multi-stage Docker builds. The runtime image is based on
the .NET 10 ASP.NET runtime (Alpine variant for minimal attack surface):

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/MyApp -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app .

# Run as non-root
RUN adduser -D -u 1000 appuser
USER appuser

EXPOSE 8080
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

:::tip[Non-root execution]
Always run containers as a non-root user. Kubernetes `securityContext.runAsNonRoot: true`
enforces this at the pod level.
:::

## Health checks with Granit.Diagnostics

Granit registers three health check endpoints conforming to Kubernetes probe conventions:

| Probe | Endpoint | Behavior |
| --- | --- | --- |
| **Liveness** | `/health/live` | Always returns 200 -- no dependency checks. Failure triggers pod restart. |
| **Readiness** | `/health/ready` | Checks dependencies tagged `"readiness"` (DB, Redis). Returns 503 on Unhealthy (pod removed from LB), 200 on Healthy or Degraded. |
| **Startup** | `/health/startup` | Checks dependencies tagged `"startup"`. Disables liveness/readiness while pending. |

### Registration

```csharp
// Program.cs
builder.Services.AddGranitDiagnostics();

var app = builder.Build();
app.MapGranitHealthChecks();
```

All three endpoints are mapped with `AllowAnonymous()` because the Kubernetes
kubelet cannot authenticate against application-level authorization.

### Adding custom health checks

Tag your checks with `"readiness"` and/or `"startup"` to include them in the
corresponding probes:

```csharp
builder.Services
    .AddHealthChecks()
    .AddNpgSql(connectionString, tags: ["readiness", "startup"])
    .AddRedis(redisConnectionString, tags: ["readiness"]);
```

## Kubernetes deployment

### Probe configuration

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: my-backend
spec:
  template:
    spec:
      containers:
        - name: app
          image: registry.example.com/my-backend:1.2.0
          ports:
            - containerPort: 8080
          livenessProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
            failureThreshold: 3
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 5
            failureThreshold: 3
          startupProbe:
            httpGet:
              path: /health/startup
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 5
            failureThreshold: 30
```

The startup probe tolerates up to 150 seconds (30 x 5s) for initial boot. This is
necessary for slow startup operations: Vault credential acquisition, EF Core
migrations, and cache warm-up.

### Resource limits

Recommended baseline for a .NET 10 application:

```yaml
resources:
  requests:
    cpu: "250m"
    memory: "256Mi"
  limits:
    cpu: "1000m"
    memory: "512Mi"
```

| Parameter | Value | Rationale |
| --- | --- | --- |
| `requests.cpu` | 250m | Minimum guarantee for GC and JIT |
| `requests.memory` | 256Mi | .NET heap + container overhead |
| `limits.cpu` | 1000m | Burst headroom for request spikes |
| `limits.memory` | 512Mi | Prevents OOM kills with margin |

:::note[Adjust for workload]
Applications that process images (`Granit.Imaging`) or large file imports
(`Granit.DataExchange`) require higher memory limits. Profile under realistic
load before setting production values.
:::

### Rolling update strategy

```yaml
spec:
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxUnavailable: 0
      maxSurge: 1
```

- **`maxUnavailable: 0`**: no pod is removed until the replacement passes readiness.
  Zero-downtime deployments.
- **`maxSurge: 1`**: one extra pod is created during the rollout.

## Graceful shutdown

Graceful shutdown is critical for applications using Wolverine. Three components
must drain in order:

1. **ASP.NET Core** stops accepting new HTTP requests and waits for in-flight
   requests to complete (5-10s).
2. **Wolverine** stops consuming from the queue, finishes running handlers,
   and commits remaining outbox messages to PostgreSQL (10-30s).
3. **Vault lease revocation** revokes dynamic credentials to minimize the
   exposure window (1-2s).

### terminationGracePeriodSeconds

```yaml
spec:
  template:
    spec:
      terminationGracePeriodSeconds: 60
```

60 seconds is appropriate for standard applications. Increase to 120 if long-running
batch operations (e.g., data migrations) are possible. If the grace period is exceeded,
Kubernetes sends SIGKILL.

## Connection pooling with PgBouncer

For multi-tenant applications with many concurrent connections, deploy PgBouncer
as a sidecar:

```yaml
containers:
  - name: pgbouncer
    image: bitnami/pgbouncer:1.22
    ports:
      - containerPort: 6432
    env:
      - name: POSTGRESQL_HOST
        value: "pg-primary.database"
      - name: POSTGRESQL_PORT
        value: "5432"
      - name: PGBOUNCER_POOL_MODE
        value: "transaction"
      - name: PGBOUNCER_MAX_CLIENT_CONN
        value: "200"
      - name: PGBOUNCER_DEFAULT_POOL_SIZE
        value: "20"
```

The application connects to `localhost:6432` instead of PostgreSQL directly.
Transaction-level pooling (`pool_mode: transaction`) is compatible with Vault
dynamic credentials because the pooler does not maintain persistent connections.

## Secrets injection

Secrets are injected via **Vault Agent Injector** or **CSI Secret Store Driver**:

```yaml
# Vault Agent Injector (annotations)
annotations:
  vault.hashicorp.com/agent-inject: "true"
  vault.hashicorp.com/role: "my-backend"
  vault.hashicorp.com/agent-inject-secret-db: "database/creds/my-readonly"
```

:::caution[No plaintext secrets]
Never store secrets in Kubernetes `ConfigMap` or `Secret` resources in plaintext.
Use Vault for centralized secret management. This is a hard requirement for
ISO 27001 compliance.
:::

## Scaling considerations

| Factor | Guidance |
| --- | --- |
| Horizontal scaling | Stateless by design -- scale replicas freely. Wolverine uses durable PostgreSQL queues, so messages are not lost during scale events. |
| Database connections | Each replica opens its own connection pool. Use PgBouncer to limit total connections to PostgreSQL. |
| Redis | All replicas share the same Redis instance for distributed cache. FusionCache (L1 in-process + L2 Redis) reduces Redis load. |
| Background jobs | `Granit.BackgroundJobs` uses Wolverine scheduling. Jobs are durable and survive pod restarts. Only one replica executes each scheduled job (leader election via PostgreSQL advisory locks). |
| Multi-tenancy | Tenant isolation is enforced at the query level (EF Core global filters). No per-tenant infrastructure is required unless data sovereignty demands physical separation. |
