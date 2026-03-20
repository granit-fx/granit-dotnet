---
title: Tech Stack
description: Complete third-party library reference for Granit — OpenTelemetry, Wolverine, EF Core, Redis, FluentValidation, and more, with ADR rationale per dependency.
sidebar:
  order: 34
---

Granit builds on battle-tested open-source libraries. This page lists every direct
production dependency, organized by functional domain. Each library was selected
through an Architecture Decision Record (ADR) when multiple alternatives existed.

For test-only dependencies, see [Testing stack (ADR-003)](/dotnet/architecture/adr/003-testing-stack/).

## Runtime and language

| Component | Version | Role |
|-----------|---------|------|
| .NET | 10 | Runtime and SDK |
| C# | 14 | Language |
| ASP.NET Core | 10 | Web framework |

## Data and persistence

| Library | License | Role | ADR |
|---------|---------|------|-----|
| [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/) | MIT | ORM, migrations, interceptors (audit, soft delete) | — |
| [Npgsql.EntityFrameworkCore.PostgreSQL](https://www.npgsql.org/) | PostgreSQL | PostgreSQL provider for EF Core | — |
| [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis) | MIT | Redis client for distributed caching | [ADR-002](/dotnet/architecture/adr/002-redis/) |
| [FusionCache](https://github.com/ZiggyCreatures/FusionCache) | MIT | L1/L2 cache with backplane, fail-safe, stampede protection, OpenTelemetry | [ADR-018](/dotnet/architecture/adr/018-fusioncache-caching-provider/) |

## Messaging and scheduling

| Library | License | Role | ADR |
|---------|---------|------|-----|
| [Wolverine](https://wolverinefx.net/) | MIT | Message bus, transactional outbox, handler pipeline | [ADR-005](/dotnet/architecture/adr/005-wolverine-cronos/) |
| [Cronos](https://github.com/HangfireIO/Cronos) | MIT | CRON expression parsing for recurring jobs | [ADR-005](/dotnet/architecture/adr/005-wolverine-cronos/) |

## Security and identity

| Library | License | Role | ADR |
|---------|---------|------|-----|
| [VaultSharp](https://github.com/rajanadar/VaultSharp) | Apache-2.0 | HashiCorp Vault client — used by `Granit.Vault.HashiCorp` (transit encryption, dynamic credentials) | — |
| [Azure.Security.KeyVault.Keys](https://learn.microsoft.com/en-us/dotnet/api/azure.security.keyvault.keys) | MIT | Azure Key Vault key operations (encrypt/decrypt) | — |
| [Azure.Security.KeyVault.Secrets](https://learn.microsoft.com/en-us/dotnet/api/azure.security.keyvault.secrets) | MIT | Azure Key Vault secret management (DB credentials) | — |
| [Azure.Identity](https://learn.microsoft.com/en-us/dotnet/api/azure.identity) | MIT | DefaultAzureCredential for Azure SDK authentication | — |
| [Microsoft.AspNetCore.Authentication.JwtBearer](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/) | MIT | JWT Bearer authentication middleware | — |

## Validation

| Library | License | Role | ADR |
|---------|---------|------|-----|
| [FluentValidation](https://docs.fluentvalidation.net/) | Apache-2.0 | Declarative validation rules | [ADR-006](/dotnet/architecture/adr/006-fluentvalidation/) |
| [SmartFormat](https://github.com/axuno/SmartFormat) | MIT | Pluralization in validation messages | [ADR-008](/dotnet/architecture/adr/008-smartformat-pluralization/) |

## AI

| Library | License | Role | ADR |
|---------|---------|------|-----|
| [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/ai-extensions) | MIT | Provider-agnostic `IChatClient` / `IEmbeddingGenerator` abstractions — used by `Granit.AI` | — |
| [Microsoft.Extensions.AI.OpenAI](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI) | MIT | OpenAI integration (GPT-4o, o3/o4-mini, embeddings) for `Granit.AI.OpenAI` and `Granit.AI.AzureOpenAI` | — |
| [Azure.AI.OpenAI](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/ai.openai-readme) | MIT | Azure OpenAI Service client with `DefaultAzureCredential` support — used by `Granit.AI.AzureOpenAI` | — |
| [Anthropic](https://www.nuget.org/packages/Anthropic) | MIT | Anthropic SDK for Claude models (Opus, Sonnet, Haiku) — used by `Granit.AI.Anthropic` | — |
| [OllamaSharp](https://github.com/awaescher/OllamaSharp) | MIT | Ollama client for local model execution — used by `Granit.AI.Ollama` | — |
| [Microsoft.Extensions.VectorData.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.VectorData.Abstractions) | MIT | Provider-agnostic vector store interface — used by `Granit.AI.VectorData` | — |

## API and web

| Library | License | Role | ADR |
|---------|---------|------|-----|
| [Asp.Versioning](https://github.com/dotnet/aspnet-api-versioning) | MIT | API versioning (URL segment, header, query) | [ADR-004](/dotnet/architecture/adr/004-asp-versioning/) |
| [Scalar](https://github.com/scalar/scalar) | MIT | Interactive OpenAPI documentation UI | [ADR-009](/dotnet/architecture/adr/009-scalar-api-documentation/) |
| [Microsoft.Extensions.Http.Resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/) | MIT | Polly v8 resilience pipeline for outbound HTTP (retry, circuit breaker, timeout) — used by HTTP-based notification providers | — |

## Observability

| Library | License | Role | ADR |
|---------|---------|------|-----|
| [Serilog](https://serilog.net/) | Apache-2.0 | Structured logging (OTLP sink) | [ADR-001](/dotnet/architecture/adr/001-observability/) |
| [OpenTelemetry .NET](https://opentelemetry.io/) | Apache-2.0 | Distributed tracing, metrics (OTLP export) | [ADR-001](/dotnet/architecture/adr/001-observability/) |

## Templating and document generation

| Library | License | Role | ADR |
|---------|---------|------|-----|
| [Scriban](https://github.com/scriban/scriban) | BSD-2-Clause | Template engine (Liquid-compatible, sandboxed) | [ADR-010](/dotnet/architecture/adr/010-scriban-template-engine/) |
| [PuppeteerSharp](https://www.puppeteersharp.com/) | MIT | HTML-to-PDF rendering via headless Chromium | [ADR-012](/dotnet/architecture/adr/012-puppeteersharp-pdf-rendering/) |
| [ClosedXML](https://github.com/ClosedXML/ClosedXML) | MIT | Excel (.xlsx) generation | [ADR-011](/dotnet/architecture/adr/011-closedxml-excel-generation/) |

## Data exchange (import/export)

| Library | License | Role | ADR |
|---------|---------|------|-----|
| [Sep](https://github.com/nietras/Sep) | MIT | High-performance CSV parsing | [ADR-015](/dotnet/architecture/adr/015-sep-csv-parsing/) |
| [Sylvan.Data.Excel](https://github.com/MarkPflworkaround/Sylvan) | MIT | Excel (.xlsx/.xls) parsing | [ADR-016](/dotnet/architecture/adr/016-sylvan-data-excel-parsing/) |

## Storage and imaging

| Library | License | Role | ADR |
|---------|---------|------|-----|
| [AWSSDK.S3](https://aws.amazon.com/sdk-for-net/) | Apache-2.0 | S3-compatible object storage (MinIO, Ceph, etc.) | — |
| [Azure.Storage.Blobs](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/storage.blobs-readme) | MIT | Azure Blob Storage client — used by `Granit.BlobStorage.AzureBlob` | — |
| [Google.Cloud.Storage.V1](https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Storage.V1/latest) | Apache-2.0 | Google Cloud Storage client — used by `Granit.BlobStorage.GoogleCloud` | — |
| [Magick.NET](https://github.com/dlemstra/Magick.NET) | Apache-2.0 | Image processing (resize, WebP/AVIF, EXIF stripping) | [ADR-013](/dotnet/architecture/adr/013-magicknet-image-processing/) |

## Notifications

| Library | License | Role | ADR |
|---------|---------|------|-----|
| [MailKit](https://github.com/jstedfast/MailKit) | MIT | SMTP email delivery | — |
| [Azure.Communication.Email](https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email) | MIT | Azure Communication Services email sending | — |
| [Azure.Communication.Sms](https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/sms/send) | MIT | Azure Communication Services SMS sending | — |
| [AWSSDK.SimpleEmailV2](https://aws.amazon.com/sdk-for-net/) | Apache-2.0 | Amazon SES v2 email delivery — used by `Granit.Notifications.Email.AwsSes` | — |
| [AWSSDK.SimpleNotificationService](https://aws.amazon.com/sdk-for-net/) | Apache-2.0 | Amazon SNS SMS delivery — used by `Granit.Notifications.Sms.AwsSns` | — |
| [Microsoft.Azure.NotificationHubs](https://learn.microsoft.com/en-us/azure/notification-hubs/) | MIT | Azure Notification Hubs push notifications | — |
| [Microsoft.AspNetCore.SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/) | MIT | Real-time WebSocket notifications | — |
| [Lib.Net.Http.WebPush](https://github.com/nicoriff/Lib.Net.Http.WebPush) | MIT | Web Push notifications (VAPID) | — |

Twilio, SendGrid, and Scaleway providers (`Granit.Notifications.Twilio`,
`Granit.Notifications.Email.SendGrid`, `Granit.Notifications.Email.Scaleway`) use the
vendors' REST APIs directly via `Microsoft.Extensions.Http.Resilience` — no vendor SDK
is required.

## Miscellaneous

| Library | License | Role | ADR |
|---------|---------|------|-----|
| [Microsoft.IO.RecyclableMemoryStream](https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream) | MIT | Pooled memory streams (reduces GC pressure) | — |

## Test-only dependencies

These libraries are used exclusively in `*.Tests` projects and are not shipped in production packages.

| Library | License | Role | ADR |
|---------|---------|------|-----|
| [xUnit v3](https://xunit.net/) | Apache-2.0 | Test framework | [ADR-003](/dotnet/architecture/adr/003-testing-stack/) |
| [Shouldly](https://docs.shouldly.org/) | BSD-3-Clause | Assertion library | [ADR-003](/dotnet/architecture/adr/003-testing-stack/), [ADR-014](/dotnet/architecture/adr/014-migration-shouldly/) |
| [NSubstitute](https://nsubstitute.github.io/) | BSD-3-Clause | Mocking framework | [ADR-003](/dotnet/architecture/adr/003-testing-stack/) |
| [Bogus](https://github.com/bchavez/Bogus) | MIT | Test data generation | [ADR-003](/dotnet/architecture/adr/003-testing-stack/) |
| [Testcontainers](https://dotnet.testcontainers.org/) | MIT | Docker-based integration tests | [ADR-007](/dotnet/architecture/adr/007-testcontainers/) |

## License summary

| License | Count | Examples |
|---------|-------|----------|
| MIT | 58 | EF Core, Wolverine, ClosedXML, StackExchange.Redis, Microsoft.Extensions.AI |
| Apache-2.0 | 18 | OpenTelemetry, Serilog, FluentValidation, Magick.NET, Google.Cloud.Storage.V1, AWSSDK.* |
| BSD-3-Clause | 2 | NSubstitute, Shouldly |
| BSD-2-Clause | 1 | Scriban |
| PostgreSQL | 1 | Npgsql |

All dependencies are OSI-approved open-source licenses compatible with Apache-2.0.
The full list with versions and copyright notices is maintained in
[`THIRD-PARTY-NOTICES.md`](https://github.com/granit-fx/granit-dotnet/blob/develop/THIRD-PARTY-NOTICES.md).
