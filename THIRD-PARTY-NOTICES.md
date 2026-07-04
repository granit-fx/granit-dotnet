# Third-Party Notices — granit-dotnet

This file lists the third-party libraries used by the **granit-dotnet**
project and their respective licenses. It is updated whenever an external
dependency is added or changed.

Only **direct dependencies** are listed here. Transitive dependencies are
covered by their own license notices, restored from NuGet by the consumer
(Granit packages do not redistribute their binaries).

Last updated: 2026-07-04 (added Roslynator.Analyzers 4.15.0, Apache-2.0 — dev-time
code-quality analyzer, `PrivateAssets="all"`, not redistributed). Prior: 2026-06-30 (global NuGet version refresh via `dotnet restore --force-evaluate`; aligned the OpenTelemetry instrumentation packages on 1.16.*; added three previously unlisted direct dependencies — DnsClient, Microsoft.Extensions.ApiDescription.Server, Npgsql.OpenTelemetry; removed dead central entries (packages referenced by no project, including modules migrated to granit-business); recomputed the license summary; notable bumps: WolverineFx 6.16.0, DocumentFormat.OpenXml 3.5.1, Scalar.AspNetCore 2.16.6, Anthropic 12.32.0, AWSSDK 4.0.100, Microsoft.\* 10.0.9 / 10.7.0, MailKit / MimeKit 4.17.0)

---

## License summary

| License      | Package count |
| ------------ | ------------- |
| MIT          | 102           |
| Apache-2.0   | 47            |
| BSD-3-Clause | 3             |
| BSD-2-Clause | 2             |
| PostgreSQL   | 2             |

---

## Production dependencies

### MIT

| Package | Version | Copyright |
| ------- | ------- | --------- |
| AngleSharp | 1.5.1 | Copyright (c) 2013-2025 AngleSharp Contributors |
| Anthropic | 12.32.0 | Copyright 2026 Anthropic |
| Asp.Versioning.Mvc | 10.0.0 | (c) .NET Foundation |
| Asp.Versioning.Mvc.ApiExplorer | 10.0.0 | (c) .NET Foundation |
| AspNet.Security.OAuth.Apple | 10.0.0 | (c) .NET Foundation |
| AspNet.Security.OAuth.GitHub | 10.0.0 | (c) .NET Foundation |
| Azure.AI.OpenAI | 2.1.0 | (c) Microsoft Corporation |
| Azure.Communication.Email | 1.1.0 | (c) Microsoft Corporation |
| Azure.Communication.Sms | 1.0.2 | (c) Microsoft Corporation |
| Azure.Identity | 1.21.0 | (c) Microsoft Corporation |
| Azure.Security.KeyVault.Keys | 4.10.0 | (c) Microsoft Corporation |
| Azure.Security.KeyVault.Secrets | 4.11.0 | (c) Microsoft Corporation |
| Azure.Storage.Blobs | 12.29.1 | (c) Microsoft Corporation |
| ClosedXML | 0.105.0 | ClosedXML Contributors |
| Cronos | 0.13.0 | Copyright (c) 2016-2025 Hangfire OU |
| DocumentFormat.OpenXml | 3.5.1 | Copyright (c) Microsoft Corporation |
| Lib.Net.Http.WebPush | 3.3.1 | Copyright (c) Tomasz Pęczek |
| MailKit | 4.17.0 | Copyright (c) 2013-2026 .NET Foundation and Contributors |
| MessagePack | 2.5.302 | Copyright (c) 2017 Yoshifumi Kawai and contributors |
| Microsoft.AspNetCore.Authentication.Facebook | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.AspNetCore.Authentication.Google | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.AspNetCore.Authentication.MicrosoftAccount | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.AspNetCore.Authentication.OpenIdConnect | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.AspNetCore.OpenApi | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.AspNetCore.OutputCaching.StackExchangeRedis | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.AspNetCore.SignalR.StackExchangeRedis | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Azure.NotificationHubs | 4.2.0 | (c) Microsoft Corporation |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | 3.3.4 | (c) Microsoft Corporation |
| Microsoft.EntityFrameworkCore | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.EntityFrameworkCore.Relational | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.AI | 10.7.0 | (c) Microsoft Corporation |
| Microsoft.Extensions.AI.Abstractions | 10.7.0 | (c) Microsoft Corporation |
| Microsoft.Extensions.AI.OpenAI | 10.7.0 | (c) Microsoft Corporation |
| Microsoft.Extensions.ApiDescription.Server | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.Caching.Abstractions | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.Caching.Hybrid | 10.7.0 | (c) Microsoft Corporation |
| Microsoft.Extensions.Caching.Memory | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.Caching.StackExchangeRedis | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.Configuration.Binder | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.Diagnostics.HealthChecks | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.Diagnostics.Testing | 10.7.0 | (c) Microsoft Corporation |
| Microsoft.Extensions.FileSystemGlobbing | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.Http | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.Http.Resilience | 10.7.0 | (c) Microsoft Corporation |
| Microsoft.Extensions.Localization | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.Localization.Abstractions | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.Logging.Abstractions | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.Options | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.VectorData.Abstractions | 10.7.0 | (c) Microsoft Corporation |
| Microsoft.IO.RecyclableMemoryStream | 3.0.1 | (c) Microsoft Corporation |
| Microsoft.Playwright | 1.61.0 | (c) Microsoft Corporation |
| MimeKit | 4.17.0 | Copyright (c) 2013-2026 .NET Foundation and Contributors |
| Mjml.Net | 4.11.0 | Copyright (c) Sebastian Stehle |
| OllamaSharp | 5.4.25 | Copyright (c) 2023-2026 Awalon |
| PdfPig | 0.1.15 | Copyright (c) Eliot Jones |
| PDFtoImage | 5.2.1 | Copyright (c) David Sungaila |
| Pgvector.EntityFrameworkCore | 0.3.0 | Copyright (c) Andrew Kane |
| PuppeteerSharp | 25.2.1 | PuppeteerSharp Contributors |
| Scalar.AspNetCore | 2.16.6 | Scalar Contributors |
| Sep | 0.15.0 | Copyright (c) 2023 nietras |
| SmartFormat | 3.6.1 | Copyright 2011-2025 SmartFormat Project |
| StackExchange.Redis | 2.13.17 | Copyright 2014-2026 Stack Exchange, Inc. |
| Sylvan.Data.Excel | 0.5.6 | Copyright (c) Mark Pflug |
| System.Composition.AttributedModel | 9.0.17 | (c) Microsoft Corporation |
| System.Text.Json | 9.0.17 | (c) Microsoft Corporation |
| WolverineFx | 6.16.0 | JasperFx Contributors |
| WolverineFx.EntityFrameworkCore | 6.16.0 | JasperFx Contributors |
| WolverineFx.FluentValidation | 6.16.0 | JasperFx Contributors |
| WolverineFx.Postgresql | 6.16.0 | JasperFx Contributors |
| WolverineFx.RuntimeCompilation | 6.16.0 | JasperFx Contributors |
| WolverineFx.SqlServer | 6.16.0 | JasperFx Contributors |
| Yarp.ReverseProxy | 2.3.0 | (c) Microsoft Corporation |
| ZiggyCreatures.FusionCache | 2.6.0 | Copyright (c) Jody Donetti |
| ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis | 2.6.0 | Copyright (c) Jody Donetti |
| ZiggyCreatures.FusionCache.OpenTelemetry | 2.6.0 | Copyright (c) Jody Donetti |
| ZiggyCreatures.FusionCache.Serialization.SystemTextJson | 2.6.0 | Copyright (c) Jody Donetti |

### Apache-2.0

| Package | Version | Copyright |
| ------- | ------- | --------- |
| AWSSDK.CognitoIdentityProvider | 4.0.100 | Amazon Web Services, Inc. |
| AWSSDK.KeyManagementService | 4.0.100 | Amazon Web Services, Inc. |
| AWSSDK.S3 | 4.0.100 | Amazon Web Services, Inc. |
| AWSSDK.SecretsManager | 4.0.100 | Amazon Web Services, Inc. |
| AWSSDK.SimpleEmailV2 | 4.0.100 | Amazon Web Services, Inc. |
| AWSSDK.SimpleNotificationService | 4.0.100 | Amazon Web Services, Inc. |
| DnsClient | 1.8.0 | Copyright (c) Michael Conrad (MichaCo) |
| Elastic.Clients.Elasticsearch | 9.4.2 | Copyright Elasticsearch B.V. |
| Fido2 | 4.0.1 | Copyright (c) 2018 Anders Åberg / passwordless-lib contributors |
| Fido2.AspNet | 4.0.1 | Copyright (c) 2018 Anders Åberg / passwordless-lib contributors |
| Fido2.Models | 4.0.1 | Copyright (c) 2018 Anders Åberg / passwordless-lib contributors |
| FirebaseAdmin | 3.5.0 | Copyright (c) 2018 Google Inc. |
| FluentValidation | 12.1.1 | Copyright (c) Jeremy Skinner, .NET Foundation 2008-2025 |
| FluentValidation.DependencyInjectionExtensions | 12.1.1 | Copyright (c) Jeremy Skinner, .NET Foundation 2008-2025 |
| Google.Cloud.Kms.V1 | 3.24.0 | Copyright (c) Google LLC |
| Google.Cloud.SecretManager.V1 | 2.7.0 | Copyright (c) Google LLC |
| Google.Cloud.Storage.V1 | 4.15.0 | Copyright (c) Google LLC |
| Magick.NET-Q8-AnyCPU | 14.14.0 | Copyright 2013-2026 Dirk Lemstra |
| MaxMind.GeoIP2 | 6.0.0 | Copyright (c) MaxMind, Inc. |
| ModelContextProtocol | 1.4.0 | Copyright (c) Anthropic, PBC and Microsoft Corporation |
| ModelContextProtocol.AspNetCore | 1.4.0 | Copyright (c) Anthropic, PBC and Microsoft Corporation |
| OpenIddict | 7.5.0 | Copyright (c) Kévin Chalet |
| OpenIddict.EntityFrameworkCore | 7.5.0 | Copyright (c) Kévin Chalet |
| OpenIddict.Server.AspNetCore | 7.5.0 | Copyright (c) Kévin Chalet |
| OpenIddict.Validation.AspNetCore | 7.5.0 | Copyright (c) Kévin Chalet |
| OpenIddict.Validation.SystemNetHttp | 7.5.0 | Copyright (c) Kévin Chalet |
| OpenTelemetry | 1.16.0 | Copyright The OpenTelemetry Authors |
| OpenTelemetry.Api | 1.16.0 | Copyright The OpenTelemetry Authors |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.16.0 | Copyright The OpenTelemetry Authors |
| OpenTelemetry.Extensions.Hosting | 1.16.0 | Copyright The OpenTelemetry Authors |
| OpenTelemetry.Instrumentation.AspNetCore | 1.16.0 | Copyright The OpenTelemetry Authors |
| OpenTelemetry.Instrumentation.AWS | 1.16.0 | Copyright The OpenTelemetry Authors |
| OpenTelemetry.Instrumentation.EntityFrameworkCore | 1.16.0-beta.1 | Copyright The OpenTelemetry Authors |
| OpenTelemetry.Instrumentation.Http | 1.16.0 | Copyright The OpenTelemetry Authors |
| OpenTelemetry.Instrumentation.StackExchangeRedis | 1.16.0-beta.1 | Copyright The OpenTelemetry Authors |
| Roslynator.Analyzers | 4.15.0 | Copyright (c) Josef Pihrt |
| Serilog.AspNetCore | 10.0.0 | Serilog Contributors |
| Serilog.Sinks.OpenTelemetry | 4.2.0 | Serilog Contributors |
| Tesseract | 5.2.0 | Copyright (c) Charles Weld |
| VaultSharp | 1.17.5.1 | Copyright (c) 2024 Raja Nadar |

### BSD-2-Clause

| Package | Version | Copyright |
| ------- | ------- | --------- |
| Markdig | 1.3.2 | Copyright (c) Alexandre Mutel |
| Scriban | 7.2.5 | Copyright (c) Alexandre Mutel |

### BSD-3-Clause

| Package | Version | Copyright |
| ------- | ------- | --------- |
| NetTopologySuite | 2.5.0 | Copyright (c) NetTopologySuite Team |

### MIT (OData)

| Package | Version | Copyright |
| ------- | ------- | --------- |
| Microsoft.AspNetCore.OData | 9.4.1 | Copyright (c) Microsoft Corporation |
| Microsoft.OData.Core | 8.4.x | Copyright (c) Microsoft Corporation |
| Microsoft.OData.Edm | 8.4.x | Copyright (c) Microsoft Corporation |

### PostgreSQL License

| Package | Version | Copyright |
| ------- | ------- | --------- |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.2 | Copyright 2025 The Npgsql Development Team |
| Npgsql.OpenTelemetry | 10.0.3 | Copyright 2025 The Npgsql Development Team |

---

## Test-only dependencies

### MIT (tests)

| Package | Version | Copyright |
| ------- | ------- | --------- |
| Bogus | 35.6.5 | Copyright (c) 2015 Brian Chavez |
| coverlet.collector | 10.0.1 | (c) 2018 Toni Solarin-Sodara |
| JunitXml.TestLogger | 7.1.0 | JunitXml.TestLogger Contributors |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.CodeAnalysis.Analyzers | 5.3.0 | (c) Microsoft Corporation |
| Microsoft.CodeAnalysis.CSharp | 5.3.0 | (c) Microsoft Corporation |
| Microsoft.CodeAnalysis.CSharp.Workspaces | 5.3.0 | (c) Microsoft Corporation |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.9 | (c) Microsoft Corporation |
| Microsoft.Extensions.TimeProvider.Testing | 10.7.0 | (c) Microsoft Corporation |
| Microsoft.NET.Test.Sdk | 18.7.0 | (c) Microsoft Corporation |
| Testcontainers.Elasticsearch | 4.12.0 | Copyright (c) 2019-2026 Andre Hofmeister and other authors |
| Testcontainers.Keycloak | 4.12.0 | Copyright (c) 2019-2026 Andre Hofmeister and other authors |
| Testcontainers.MsSql | 4.12.0 | Copyright (c) 2019-2026 Andre Hofmeister and other authors |
| Testcontainers.PostgreSql | 4.12.0 | Copyright (c) 2019-2025 Andre Hofmeister |
| Testcontainers.Redis | 4.12.0 | Copyright (c) 2019-2025 Andre Hofmeister |

### Apache-2.0 (tests)

| Package | Version | Copyright |
| ------- | ------- | --------- |
| SQLitePCLRaw.bundle_e_sqlite3 | 3.0.3 | Copyright (c) SourceGear, LLC (Eric Sink) |
| SQLitePCLRaw.core | 3.0.3 | Copyright (c) SourceGear, LLC (Eric Sink) |
| TngTech.ArchUnitNET | 0.13.3 | Copyright (c) 2019-2025 TNG Technology Consulting GmbH |
| TngTech.ArchUnitNET.xUnit | 0.13.3 | Copyright (c) 2019-2025 TNG Technology Consulting GmbH |
| WireMock.Net | 2.11.0 | Copyright (c) WireMock.Net Contributors |
| xunit.v3 | 3.2.2 | Copyright (C) .NET Foundation |
| xunit.runner.visualstudio | 3.1.5 | Copyright (C) .NET Foundation |

### BSD-3-Clause (tests)

| Package     | Version | Copyright                                |
| ----------- | ------- | ---------------------------------------- |
| NSubstitute | 5.3.0   | NSubstitute Contributors                 |
| Shouldly    | 4.3.0   | Copyright (c) 2017 Shouldly Contributors |

---

## External binary dependencies (runtime)

### LibreOffice

| Field | Value |
| --- | --- |
| Name | LibreOffice |
| Version | 7.x+ (recommended) |
| License | MPL-2.0 (binary) + LGPL-3.0 (components) |
| Copyright | © The Document Foundation |
| Added | 2026-05-11 |

The `soffice` binary is invoked by `Granit.Documents.Renditions.Office` via
`Process.Start` (`--headless --convert-to pdf` mode). No LibreOffice code is
compiled, linked or redistributed with the framework — the host installs the
dependency on its runtime image (`apt-get install libreoffice` /
`apk add libreoffice` / `brew install --cask libreoffice`). The framework
remains under Apache-2.0.

### Geo-IP databases (MaxMind GeoLite2 / DB-IP)

| Field | Value |
| --- | --- |
| Name | MaxMind GeoLite2 / DB-IP Lite (`.mmdb` files) |
| License | GeoLite2: MaxMind EULA + CC BY-SA 4.0 (attribution) — DB-IP Lite: CC BY 4.0 |
| Copyright | © MaxMind, Inc. / © db-ip.com |
| Added | 2026-06-12 |

The database file is read read-only by the `Granit.IpGeolocation.MaxMind`
module (offline IP geolocation provider). The `.mmdb` file is **supplied and
provisioned by the consumer** (downloaded from MaxMind or DB-IP); it is
**neither embedded nor redistributed** by the framework. The attribution
obligations (CC BY-SA 4.0 / CC BY 4.0) and compliance with the MaxMind EULA
fall on the deployment that installs the database. The framework remains under
Apache-2.0.

---

## Embedded datasets

### Franc trigram dataset

| Field | Value |
| --- | --- |
| Source | <https://github.com/wooorm/franc> (`packages/franc-all/data.js`) |
| Author | Titus Wormer, 2014+ |
| License | MIT |
| Copyright | © 2014 Titus Wormer ; © 2008 Kent S Johnson ; © 2006 Jacob R Rideout |
| Added | 2026-05-26 |

The `src/Granit.LanguageDetection.Trigram/Resources/profiles.json` file is
derived from the Franc dataset (parsed from the JS source into compact JSON,
~549 KB). The trigram profiles were trained on the Universal Declaration of
Human Rights (UDHR) corpus and Wikipedia excerpts. The detector's C# code is a
**clean-room** reimplementation; only the statistical data (ranked trigram
tables) is reused. The MIT license allows inclusion without Apache-2.0
contamination of the framework, provided this attribution is preserved.

The original format (`data.js` JS) is documented at
<https://github.com/wooorm/franc/tree/main/packages/franc-all>.

---

## Compliance notes

### AWSSDK.S3

This SDK is used solely for S3 compatibility with object storage hosted in
Europe (S3-compatible object storage, S3-compatible API).

### AWSSDK.SimpleEmailV2

This SDK provides an email delivery channel via Amazon SES. It is used by the
`Granit.Notifications.Email.AwsSes` package as an alternative to the SMTP channel.

### AWSSDK.SimpleNotificationService

This SDK is used by the `Granit.Notifications.Push.Aws` package to send mobile
push notifications via Amazon SNS as an alternative to Firebase Cloud Messaging
and Azure Notification Hubs.

### AWSSDK.CognitoIdentityProvider

This SDK is used by the `Granit.Identity.Federated.Cognito` package to administer
AWS Cognito User Pool users (CRUD, groups, sessions, passwords) as an alternative
to the Keycloak provider.

### AWSSDK.KeyManagementService / AWSSDK.SecretsManager

These SDKs are used by the `Granit.Vault.Aws` package for transit encryption
(KMS) and database credential management (Secrets Manager) as an alternative to
the HashiCorp Vault provider.

### Azure.AI.OpenAI

This SDK is used by the `Granit.*.AI` modules for integration with Azure OpenAI
Service. Data is processed in the Azure region configured by the deployment. No
user content is used to train the models.

### Azure.Security.KeyVault.Keys / Azure.Security.KeyVault.Secrets

These SDKs are used by the `Granit.Vault.Azure` package for transit encryption
(Key Vault RSA) and database credential management (Key Vault Secrets) as an
alternative to the HashiCorp Vault provider.

### Azure.Storage.Blobs

This SDK is used by the `Granit.BlobStorage.AzureBlob` package for binary object
storage via Azure Blob Storage as an alternative to the S3 and Google Cloud
Storage providers.

### Azure.Communication.Email / Azure.Communication.Sms

These SDKs are used by the `Granit.Notifications.Email.AzureCommunicationServices`
and `Granit.Notifications.Sms.AzureCommunicationServices` packages to send emails
and SMS via Azure Communication Services.

### FirebaseAdmin

This SDK is used by the `Granit.Identity.Firebase` (Firebase Authentication user
administration) and `Granit.Notifications.Push.Firebase` (push notification
delivery via Firebase Cloud Messaging) packages.

### Google.Cloud.Kms.V1 / Google.Cloud.SecretManager.V1

These SDKs are used by the `Granit.Vault.GoogleCloud` package for transit
encryption (Cloud KMS) and database credential management (Secret Manager) as an
alternative to the HashiCorp Vault provider.

### Google.Cloud.Storage.V1

This SDK is used by the `Granit.BlobStorage.GoogleCloud` package for binary object
storage via Google Cloud Storage as an alternative to the S3 and Azure Blob
Storage providers.

### ipinfo.io (Granit.IpGeolocation.IpApi)

The optional `Granit.IpGeolocation.IpApi` package queries the third-party
[ipinfo.io](https://ipinfo.io) API to resolve an IP address into an approximate
location. **GDPR:** an IP address is personal data transmitted to a processor, so
this provider is **disabled by default** and only activates on explicit
registration (`AddGranitIpGeolocationIpApi()`) and inclusion in
`IpGeolocation:ProviderOrder`. For sensitive deployments, prefer the offline
`Granit.IpGeolocation.MaxMind` provider, which emits no external calls. No
ipinfo.io library is compiled or redistributed (HTTP calls via
`IHttpClientFactory`).

### Microsoft.Azure.NotificationHubs

This SDK is used by the `Granit.Notifications.MobilePush.AzureNotificationHubs`
package to send mobile push notifications (FCM, APNS) via Azure Notification Hubs.

### Microsoft.Playwright

This SDK is used by the `Granit.Browsing.Playwright` package to drive a headless
browser (PDF rendering, screenshots, internal scraping). The browser binary is
downloaded by Playwright on first run on the runtime image — no Chromium binary
is redistributed with the framework.

### MimeKit

This package is used by `Granit.Notifications.Email.Smtp` to build MIME messages
and by `Granit.TextExtraction.Email` to extract text from `.eml` files (RFC822).
The version is pinned to 4.16.0 via `Directory.Packages.props` to fix the
GHSA-g7hc-96xr-gvvx vulnerability (CVE affecting versions < 4.15.1) and to align
with the transitive requirement of MailKit 4.16.0.

### OllamaSharp

This SDK is used by the `Granit.*.AI` modules for integration with local language
models via Ollama. Data stays on the organization's infrastructure (no calls to
external cloud services).

### Tesseract

This package is used by `Granit.TextExtraction.Ocr.Tesseract` as a local OCR
engine for raster images (PNG, JPEG, TIFF, BMP). A .NET wrapper around the native
`libtesseract` library (Apache-2.0), which must be installed separately on the
host (`apt-get install libtesseract5 tesseract-ocr-{lang}`). No data leaves the
host — it is the on-prem counterpart of the `Granit.TextExtraction.Ocr.AI` module
(which relies on a third-party VLM).

### Yarp.ReverseProxy

This package is used by `Granit.Bff.Yarp` for the Backend for Frontend (BFF)
reverse proxy. It routes frontend API calls to backend microservices without
exposing access tokens to the browser.
