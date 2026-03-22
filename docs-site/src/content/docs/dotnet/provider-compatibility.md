---
title: Provider Compatibility Matrix
description: Database, cache, storage, and infrastructure provider support matrix for Granit modules
sidebar:
  label: Provider Compatibility
  order: 33
---

Granit is developed and tested against PostgreSQL and the Grafana LGTM observability
stack. Other providers are supported through EF Core's provider abstraction or
dedicated packages, but receive varying levels of testing.

This page documents the current support status for each infrastructure dimension.

## Legend

| Status | Meaning |
|--------|---------|
| **Supported** | Actively tested in CI (unit + integration tests) |
| **Compatible** | Uses provider-agnostic EF Core APIs; expected to work but not tested in CI |
| **Partial** | Known limitations or missing features for this provider |
| **Provider package** | Requires installing a dedicated Granit provider package |
| **N/A** | Module does not use this infrastructure |

## Summary matrix

| Infrastructure | Primary (tested) | Secondary (compatible) |
|----------------|-------------------|------------------------|
| Database | PostgreSQL | SQL Server, SQLite (via EF Core) |
| Cache | Memory (`IDistributedCache`) | Redis (StackExchange.Redis), FusionCache (L1+L2) |
| Blob storage | S3-compatible (MinIO, AWS S3) | Azure Blob, Google Cloud Storage |
| Identity provider | Keycloak | Entra ID (Azure AD), AWS Cognito, Google Cloud Identity Platform, custom (`IIdentityProvider`) |
| Messaging | PostgreSQL (Wolverine) | SQL Server (Wolverine), RabbitMQ (Wolverine) |
| Observability | Grafana LGTM (Loki/Tempo/Mimir) | Any OTLP-compatible backend |
| Encryption | HashiCorp Vault Transit | Azure Key Vault (`Granit.Vault.Azure`), AWS KMS (`Granit.Vault.Aws`), Google Cloud KMS (`Granit.Vault.GoogleCloud`) |

## Database providers

All `*.EntityFrameworkCore` packages use EF Core's provider-agnostic APIs and do not
contain provider-specific SQL. The database provider is chosen by the application at
registration time (e.g., `opts.UseNpgsql(connectionString)`).

PostgreSQL is the only provider with integration tests. SQL Server is expected to work
based on EF Core compatibility, but is not verified in CI. SQLite may work for
development scenarios but lacks support for some features used by Granit (e.g.,
`DateTimeOffset` handling, concurrent migrations).

| Module | PostgreSQL | SQL Server | SQLite |
|--------|-----------|------------|--------|
| Granit.Persistence | Supported | Compatible | Compatible |
| Granit.Persistence.Migrations | Supported | Compatible | Not tested |
| Granit.Authorization.EntityFrameworkCore | Supported | Compatible | Compatible |
| Granit.Settings.EntityFrameworkCore | Supported | Compatible | Compatible |
| Granit.Features.EntityFrameworkCore | Supported | Compatible | Compatible |
| Granit.Identity.Federated.EntityFrameworkCore | Supported | Compatible | Compatible |
| Granit.Localization.EntityFrameworkCore | Supported | Compatible | Compatible |
| Granit.ReferenceData.EntityFrameworkCore | Supported | Compatible | Compatible |
| Granit.BlobStorage.EntityFrameworkCore | Supported | Compatible | Compatible |
| Granit.Notifications.EntityFrameworkCore | Supported | Compatible | Compatible |
| Granit.Webhooks.EntityFrameworkCore | Supported | Compatible | Compatible |
| Granit.Workflow.EntityFrameworkCore | Supported | Compatible | Compatible |
| Granit.Timeline.EntityFrameworkCore | Supported | Compatible | Compatible |
| Granit.Templating.EntityFrameworkCore | Supported | Compatible | Compatible |
| Granit.DataExchange.EntityFrameworkCore | Supported | Compatible | Compatible |
| Granit.BackgroundJobs.EntityFrameworkCore | Supported | Compatible | Compatible |
| Granit.Querying.EntityFrameworkCore | Supported | Compatible | Compatible |
| Granit.Authentication.ApiKeys.EntityFrameworkCore | Supported | Compatible | Compatible |

### Wolverine database transports

Wolverine's transactional outbox and durable messaging require a provider-specific
package. These are **not** interchangeable via EF Core abstractions.

| Package | PostgreSQL | SQL Server |
|---------|-----------|------------|
| Granit.Wolverine.Postgresql | Supported | N/A |
| Granit.Wolverine.SqlServer | N/A | Provider package |

`Granit.Wolverine.Postgresql` is the primary and fully tested transport.
`Granit.Wolverine.SqlServer` exists as a dedicated package but receives less testing.

### Notes on provider selection

- **PostgreSQL** is the recommended and fully supported provider. All integration tests,
  CI pipelines, and production deployments use PostgreSQL.
- **SQL Server** should work for all EF Core modules because Granit avoids
  provider-specific SQL. The `Granit.Wolverine.SqlServer` package provides the
  Wolverine transport for SQL Server.
- **SQLite** is suitable only for local development or unit tests. It does not support
  concurrent migrations, and some EF Core behaviors differ (e.g., `DateTimeOffset`
  storage). Granit unit tests use `Microsoft.EntityFrameworkCore.InMemory`, not SQLite.

## Cache providers

Granit.Caching provides a layered caching architecture. The base module registers
`IDistributedCache` with an in-memory implementation. Provider packages replace or
augment this.

| Package | Provider | Role |
|---------|----------|------|
| Granit.Caching | FusionCache (L1 memory + L2 distributed) | Default with in-memory only, stampede protection built-in |
| Granit.Caching.StackExchangeRedis | Redis (StackExchange.Redis) | Adds Redis as L2 distributed backplane |
| Granit.Caching.FusionCache | FusionCache with Redis | FusionCache with Redis L2 and backplane |

### Cache encryption

When `CachingOptions.EncryptValues` is enabled, `Granit.Caching.StackExchangeRedis`
automatically applies AES-256 encryption to cached values at rest in Redis.

### Modules that consume caching

The following modules use `IFusionCache` internally and benefit from provider upgrades:

- Granit.Features (FusionCache for feature flag resolution)
- Granit.Http.Idempotency (Redis required for distributed idempotency keys)
- Granit.RateLimiting (Redis required for distributed rate limiting)

## Storage providers

| Package | Provider | Status |
|---------|----------|--------|
| Granit.BlobStorage.S3 | Any S3-compatible API (MinIO, AWS S3, Scaleway, etc.) | Supported |
| Granit.BlobStorage.AzureBlob | Azure Blob Storage | Supported |
| Granit.BlobStorage.GoogleCloud | Google Cloud Storage | Supported |

Granit.BlobStorage uses a Direct-to-Cloud architecture with presigned URLs. The
`Granit.BlobStorage.S3` package implements this via AWSSDK.S3. Any service that
exposes an S3-compatible API is supported.

There is no local filesystem provider. For local development, use MinIO in a container.

## Identity providers

| Package | Provider | Status |
|---------|----------|--------|
| Granit.Authentication.Keycloak | Keycloak | Supported |
| Granit.Authentication.EntraId | Microsoft Entra ID (Azure AD) | Supported |
| Granit.Authentication.Cognito | AWS Cognito | Supported |
| Granit.Authentication.GoogleCloud | Google Cloud Identity Platform (Firebase Auth) | Supported |
| Granit.Authentication.JwtBearer | Any OIDC/JWT issuer | Supported (base layer) |
| Granit.Identity.Federated.Keycloak | Keycloak Admin API | Supported |
| Granit.Identity.Federated.EntraId | Microsoft Graph API | Supported |
| Granit.Identity.Federated.Cognito | AWS Cognito User Pool API | Supported |
| Granit.Identity.Federated.GoogleCloud | Firebase Admin SDK | Supported |
| Granit.Identity (abstractions) | Custom via `IIdentityProvider` | Implement your own |

### Authentication vs. identity

- **Authentication** packages handle JWT validation and claims transformation at the
  HTTP middleware level.
- **Identity** packages implement `IIdentityProvider` for user lookup, role management,
  and cache synchronization via the Admin APIs of each provider.

Keycloak, Entra ID, AWS Cognito, and Google Cloud Identity Platform all have
dedicated packages for both layers.

## Messaging transports

| Package | Transport | Status |
|---------|-----------|--------|
| Granit.Wolverine.Postgresql | PostgreSQL (Wolverine durable messaging) | Supported |
| Granit.Wolverine.SqlServer | SQL Server (Wolverine durable messaging) | Provider package |
| Granit.Wolverine | In-process (Wolverine local queues) | Supported |

### RabbitMQ

Wolverine natively supports RabbitMQ via the `WolverineFx.RabbitMQ` package. Granit
does not provide a wrapper package for RabbitMQ because no Granit-specific configuration
is needed. Add the Wolverine RabbitMQ package directly in your application if needed.

### Modules that publish messages

The following modules integrate with Wolverine for asynchronous processing:

- Granit.Notifications.Wolverine (notification fan-out)
- Granit.Webhooks.Wolverine (webhook delivery)
- Granit.DataExchange.Wolverine (import job processing)
- Granit.BackgroundJobs.Wolverine (scheduled job execution)
- Granit.Persistence.Migrations.Wolverine (migration progress tracking)
- Granit.Privacy (GDPR erasure cascading)

## Notification channels

| Package | Channel | External dependency |
|---------|---------|---------------------|
| Granit.Notifications.Email.Smtp | Email (SMTP) | Any SMTP server (MailKit) |
| Granit.Notifications.Email.AwsSes | Email (AWS) | Amazon Simple Email Service |
| Granit.Notifications.Email.AzureCommunicationServices | Email (Azure) | Azure Communication Services |
| Granit.Notifications.Brevo | Email, SMS, WhatsApp | Brevo Transactional API |
| Granit.Notifications.Email.SendGrid | Email (SendGrid) | SendGrid API (Twilio) |
| Granit.Notifications.Email.Scaleway | Email (Scaleway) | Scaleway Transactional Email |
| Granit.Notifications.Sms | SMS (abstractions) | Requires a provider (Brevo) |
| Granit.Notifications.Sms.AzureCommunicationServices | SMS (Azure) | Azure Communication Services |
| Granit.Notifications.WhatsApp | WhatsApp (abstractions) | Requires a provider (Brevo) |
| Granit.Notifications.WebPush | Web Push | VAPID keys (Lib.Net.Http.WebPush) |
| Granit.Notifications.MobilePush.GoogleFcm | Mobile Push (FCM) | Firebase Cloud Messaging |
| Granit.Notifications.MobilePush.AzureNotificationHubs | Mobile Push (Azure) | Azure Notification Hubs |
| Granit.Notifications.Sms.AwsSns | SMS (AWS) | Amazon SNS |
| Granit.Notifications.MobilePush.AwsSns | Mobile Push (AWS) | Amazon SNS platform applications |
| Granit.Notifications.Twilio | SMS, WhatsApp (Twilio) | Twilio Messaging API |
| Granit.Notifications.SignalR | Real-time (WebSocket) | Redis backplane for multi-pod |

### Provider coverage

Brevo and Twilio are multi-channel providers. Brevo implements email, SMS, and WhatsApp
through a single integration. Twilio covers SMS and WhatsApp via the Twilio Messaging API,
while SendGrid (a Twilio company) provides a dedicated email provider. The SMTP provider
is an alternative for email-only scenarios.

The SMS and WhatsApp abstraction packages define `ISmsSender` and `IWhatsAppSender`
interfaces. Additional providers can be implemented against these interfaces.

Azure Communication Services provides email and SMS through two dedicated packages.
Azure Notification Hubs provides multi-platform mobile push.
Amazon SNS provides SMS and mobile push through two dedicated packages.

See also: [Cloud Providers](/dotnet/cloud-providers/) for all packages organized
by cloud provider.

## Observability backends

| Signal | Package | Export protocol |
|--------|---------|----------------|
| Logs | Serilog + Serilog.Sinks.OpenTelemetry | OTLP |
| Traces | OpenTelemetry.Instrumentation.AspNetCore/Http/EFCore | OTLP |
| Metrics | OpenTelemetry | OTLP |

All three signals are exported via OTLP (OpenTelemetry Protocol). The target backend
is configurable at the application level.

### Tested backends

| Backend | Signal | Status |
|---------|--------|--------|
| Grafana Loki | Logs | Supported (production) |
| Grafana Tempo | Traces | Supported (production) |
| Grafana Mimir | Metrics | Supported (production) |
| Any OTLP-compatible collector | All | Compatible |

Because Granit exports standard OTLP, any backend that accepts OTLP should work
(e.g., Datadog, New Relic, Elastic APM, Jaeger). Only the Grafana LGTM stack is
verified in production.

## Infrastructure services used by specific modules

Some modules require specific infrastructure services regardless of provider choice.

| Module | Requires |
|--------|----------|
| Granit.Vault.HashiCorp | HashiCorp Vault (VaultSharp) |
| Granit.Vault.Azure | Azure Key Vault (DefaultAzureCredential) |
| Granit.Vault.Aws | AWS KMS + Secrets Manager |
| Granit.Vault.GoogleCloud | Google Cloud KMS + Secret Manager |
| Granit.Http.Idempotency | Redis (StackExchange.Redis) |
| Granit.RateLimiting | Redis (StackExchange.Redis) |
| Granit.Notifications.SignalR | Redis backplane (for multi-pod deployments) |
| Granit.BlobStorage.S3 | S3-compatible object storage |
| Granit.BlobStorage.AzureBlob | Azure Blob Storage |
| Granit.BlobStorage.GoogleCloud | Google Cloud Storage |
