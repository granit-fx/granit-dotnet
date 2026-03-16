---
title: Production checklist
description: Go-live verification for security, GDPR, ISO 27001, and operational readiness
sidebar:
  order: 5
---

This checklist covers the mandatory verifications before deploying a Granit
application to production. Every item maps to a compliance requirement
(GDPR, ISO 27001) or an operational best practice.

## Security hardening

- [ ] No plaintext secrets in code, configuration files, or unencrypted environment variables
- [ ] HashiCorp Vault or Azure Key Vault configured and reachable
- [ ] PostgreSQL credentials are dynamic via Vault or Key Vault Secrets (no static passwords)
- [ ] HTTPS enforced -- no HTTP endpoints in production
- [ ] JWT Bearer configured with `RequireHttpsMetadata: true`
- [ ] RBAC permissions defined and assigned per tenant
- [ ] No debug or diagnostic endpoints exposed publicly
- [ ] CORS policy restricted to known origins (`Granit.Cors`)
- [ ] Rate limiting configured for public-facing endpoints
- [ ] TLS between all internal components (application to database, application to Redis, application to Vault)

## ISO 27001 compliance

- [ ] Audit trail enabled: `AuditedEntityInterceptor` registered in every DbContext
- [ ] `CreatedBy`, `ModifiedBy` populated automatically via `ICurrentUserService`
- [ ] Log retention configured to 3 years minimum in Loki (cold storage for archive)
- [ ] Encryption at rest enabled for sensitive data (Vault Transit or Azure Key Vault via `IStringEncryptionService`)
- [ ] Encryption in transit: TLS between all components
- [ ] Access traceability: every request associated with `UserId` and `TenantId` in logs
- [ ] Authentication events logged (login, logout, token refresh, failed attempts)

## GDPR compliance

- [ ] Soft delete enabled for entities containing personal data (`FullAuditedEntity`)
- [ ] `SoftDeleteInterceptor` registered in the DbContext
- [ ] EF Core global query filters active (deleted entities hidden by default via `ApplyGranitConventions`)
- [ ] Data minimization verified: no superfluous fields in entities
- [ ] Pseudonymization: personal data encrypted via Vault Transit
- [ ] Right to erasure: process documented and tested
- [ ] Data processing records maintained (`Granit.Privacy`)

## Database

- [ ] EF Core migrations applied and tested in a staging environment first
- [ ] Indexes created on frequently filtered columns (`TenantId`, `IsDeleted`, `CreatedAt`)
- [ ] Connection pooling configured (PgBouncer sidecar for multi-tenant applications)
- [ ] Automated backups configured and restoration tested
- [ ] Dynamic Vault credentials functional (lease renewal verified end-to-end)
- [ ] Connection string does not contain static credentials

## Observability

- [ ] `Observability:OtlpEndpoint` configured to point at the OpenTelemetry Collector
- [ ] `Observability:ServiceName` set to a meaningful service identifier
- [ ] `Observability:ServiceVersion` set to the deployed version
- [ ] Grafana dashboards provisioned (HTTP, database, cache, Wolverine)
- [ ] Alert rules configured (error rate, latency, Vault failures, dead letter queue)
- [ ] Log-trace correlation verified: Loki to Tempo link functional in Grafana
- [ ] All telemetry routed to European infrastructure (no US-based collectors)

## Kubernetes

- [ ] Liveness probe configured (`/health/live`)
- [ ] Readiness probe configured (`/health/ready`)
- [ ] Startup probe configured (`/health/startup`)
- [ ] Resource limits defined (CPU and memory requests/limits)
- [ ] `terminationGracePeriodSeconds` set to 60s minimum
- [ ] Rolling update strategy: `maxUnavailable: 0`
- [ ] Secrets injected via Vault Agent (no plaintext Kubernetes Secrets)
- [ ] Pod runs as non-root user (`securityContext.runAsNonRoot: true`)
- [ ] Network policies restrict pod-to-pod communication to required paths

## Cache (Redis)

- [ ] Redis accessible and password-protected (credentials from Vault)
- [ ] `[CacheEncrypted]` applied on types containing sensitive data
- [ ] TTL configured to prevent memory exhaustion
- [ ] Stampede protection active (Granit default configuration)

## Messaging (Wolverine)

- [ ] PostgreSQL transport configured (`Granit.Wolverine.Postgresql`)
- [ ] Outbox tables created (`wolverine_outbox_*`)
- [ ] Context propagation verified (`TenantId`, `UserId`, `traceparent` flow through message handlers)
- [ ] Dead letter queue monitored (alert rule if non-empty)
- [ ] Graceful shutdown verified: all handlers complete before SIGKILL

## Performance

- [ ] Load tests executed with a realistic traffic profile
- [ ] Performance baseline established (p50, p95, p99 latency)
- [ ] No N+1 query patterns identified in EF Core traces
- [ ] Cache hit ratio acceptable (> 80% for frequently accessed data)
- [ ] Database query plans reviewed for critical paths

## Operational readiness

- [ ] Runbook written covering operational procedures (startup, shutdown, incident response)
- [ ] Deployment architecture documented (network diagram, component inventory)
- [ ] On-call contacts defined and notification channels tested
- [ ] Rollback procedure documented and tested
- [ ] Change management process followed (MR approved, CI passed, staging validated)

:::tip[Staged rollout]
Deploy to a staging environment first and run the full checklist there. Production
deployment should only proceed after staging validation is complete and signed off
by the SRE team.
:::

## Post-deployment verification

After the first production deployment, verify:

- [ ] Health check endpoints return 200 (`/health/live`, `/health/ready`, `/health/startup`)
- [ ] Logs appear in Grafana/Loki with correct `ServiceName` and `Environment`
- [ ] Traces appear in Grafana/Tempo for HTTP requests
- [ ] Metrics appear in Grafana/Mimir (request rate, error rate)
- [ ] Vault lease renewal (or Azure Key Vault secret rotation) succeeds (check logs for confirmation)
- [ ] At least one end-to-end request completes successfully (smoke test)
