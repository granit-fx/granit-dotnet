# Changelog

Tous les changements notables de ce projet seront documentés dans ce fichier.

Le format est basé sur [Keep a Changelog](https://keepachangelog.com/fr/1.0.0/),
et ce projet adhère au [Semantic Versioning](https://semver.org/lang/fr/).

## [Unreleased]

### Changed (framework — BREAKING: behavior)

- **Auditing retention cleanup is now a distributed recurring job, not a per-pod hosted service.** The `AuditingCleanupWorker` `BackgroundService` — which ran on **every** replica and purged the same table N× in parallel on a multi-replica deployment — is replaced by `AuditRetentionCleanupJob` in the new **`Granit.Auditing.BackgroundJobs`** package: a `[RecurringJob]` that runs **once cluster-wide** via the distributed scheduler. The `AuditingOptions.CleanupInterval` option is **removed** (superseded by the job cron, default `0 2 * * *`, overridable via `BackgroundJobs:Jobs:auditing-retention-cleanup`); `CleanupBatchSize` and the per-category `*Retention` options are unchanged. Apps that want retention cleanup must register `GranitAuditingBackgroundJobsModule` (depends on `Granit.Auditing.EntityFrameworkCore` + `Granit.BackgroundJobs`).
- **Auditing now persists synchronously (`Strict`) by default.** `AuditPersistenceMode.Strict` is now both the default value of `AuditingOptions.PersistenceMode` *and* the enum's zero value, so an unconfigured options instance is durable by default — the right posture for an ISO 27001 / GDPR audit trail. The previous default (`Async`) buffers entries in an in-memory channel and could silently drop them on shutdown. Only entity-change capture (via the EF Core interceptor) is affected; explicit writes through `IAuditingWriter` were already synchronous. Throughput-sensitive apps must now opt into async buffering explicitly. Reordering the enum's underlying int values is wire-safe (the enum is persisted/serialized by name).

  **Migration** — to keep the previous async-buffered behavior:

  ```json
  "Auditing": { "PersistenceMode": "Async" }
  ```

  When running in `Async` mode under Kubernetes, also raise the host shutdown timeout so the graceful drain (below) can flush the buffer before SIGKILL — the .NET default is only 5s, well under the typical 30s `terminationGracePeriodSeconds`:

  ```csharp
  builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(25));
  ```

### Changed (framework — BREAKING: appsettings)

- **`SectionName` homogenization across the framework.** Every `public const string SectionName` on a Granit options class now follows the same convention: a colon-separated, ASP.NET-style hierarchical path aligned on the project namespace after stripping the internal `Granit` prefix (`Granit.Foo.Bar.Baz` → `"Foo:Bar:Baz"`). Three classes of legacy values were eliminated: (1) the `Granit:` root prefix (which was leaking the code namespace into config — `"Granit:ApiKeys"`, `"Granit:IO:TempFiles"`, `"Granit:Templating:App"`, `"Granit:DataExchange:BlobStorage"`); (2) PascalCase-glued compound names that hid a real hierarchy (`"WolverinePostgresql"`, `"WolverineSqlServer"`, `"GranitMigrations"`, `"TenantSchema"`, every `"*Endpoints"` umbrella, every `Http.*` flat name); (3) vendor-rooted or wrong-segmented names under `Notifications.*` and `Identity.Federated.*` (`"AzureCommunicationServices:Email"`, `"Notifications:Smtp"`, `"KeycloakAdmin"`, `"EntraIdAdmin"`, `"CognitoAdmin"`, `"Identity:UserCacheHasher"`, …). One real binding collision was also fixed: `ImportOptions` and the root data-exchange config both bound to `"DataExchange"`, silently shadowing one another — `ImportOptions` now lives at `"DataExchange:Import"` and `ExportOptions` at `"DataExchange:Export"`. `TenantIsolationOptions` (which had no `SectionName` const and was bound to the literal `"TenantIsolation"`) now exposes the const and binds to `"MultiTenancy:TenantIsolation"`, aligned with `MultiTenancy:TenantSchema`. The convention is enforced going forward by `Granit.ArchitectureTests.SectionNameConventionTests` (forbids `Granit:` root, forbids flat compound names, requires SectionName uniqueness). Every renamed section also has its `*OptionsTests.SectionName.ShouldBe(...)` assertion updated, every test that builds an in-memory `IConfiguration` for a service-collection extension was migrated to the new prefix (otherwise the binder fails silently and tests assert default values), and the `templates/granit-api-full/appsettings.json` was repointed (`"Cors"` → `"Http:Cors"`). The Keycloak JwtBearer rename (`"Keycloak"` → `"Authentication:Keycloak"`) shipped earlier in this release is part of the same sweep.

  **Migration** — rewrite the host `appsettings.json` (only the sections you actually use):

  ```diff
  - "Keycloak":              { ... }      // JwtBearer
  + "Authentication": { "Keycloak": { ... } }

  - "Cognito":               { ... }
  - "EntraId":               { ... }
  - "GoogleCloudAuth":       { ... }
  + "Authentication": { "Cognito": { ... }, "EntraId": { ... }, "GoogleCloud": { ... } }

  - "Granit:ApiKeys":        { ... }
  + "Authentication": { "ApiKeys": { ... } }

  - "WolverinePostgresql":   { ... }
  - "WolverineSqlServer":    { ... }
  + "Wolverine": { "Postgresql": { ... }, "SqlServer": { ... } }

  - "GranitMigrations":      { ... }
  + "Persistence": { "Migrations": { ... } }

  - "TenantSchema":          { ... }
  + "MultiTenancy": { "TenantSchema": { ... } }

  - "TokenManagement":       { ... }
  + "Oidc": { "TokenManagement": { ... } }

  - "ReEncryption":          { ... }
  + "Vault": { "ReEncryption": { ... } }

  - "Granit:IO:TempFiles":   { ... }
  + "IO": { "TempFiles": { ... } }

  - "Granit:Templating:App": { ... }
  + "Templating": { "App": { ... } }

  - "Granit:DataExchange:BlobStorage": { ... }
  + "DataExchange": { "BlobStorage": { ... } }

  - "TenantIsolation":      { ... }
  + "MultiTenancy": { "TenantIsolation": { ... } }

  # *.Endpoints umbrella sections (one example, applies to every Endpoints module):
  - "BlobStorageEndpoints":  { ... }
  + "BlobStorage": { "Endpoints": { ... } }
  - "AIEndpoints":           { ... }
  + "AI": { "Endpoints": { ... } }
  - "ApiKeysEndpoints":      { ... }
  + "Authentication": { "ApiKeys": { "Endpoints": { ... } } }
  - "AuthorizationEndpoints":{ ... }
  + "Authorization": { "Endpoints": { ... } }
  - "BackgroundJobsEndpoints":{ ... }
  + "BackgroundJobs": { "Endpoints": { ... } }
  - "DataExchangeEndpoints": { ... }
  + "DataExchange": { "Endpoints": { ... } }
  - "IdentityEndpoints":     { ... }
  - "IdentityProviderEndpoints": { ... }
  - "IdentityWebhook":       { ... }
  + "Identity": { "Endpoints": { ... , "Provider": { ... } }, "Webhook": { ... } }
  - "SchedulingEndpoints":   { ... }
  + "Scheduling": { "Endpoints": { ... } }
  - "TimelineEndpoints":     { ... }
  + "Timeline": { "Endpoints": { ... } }
  - "WebhooksEndpoints":     { ... }
  + "Webhooks": { "Endpoints": { ... } }
  - "WorkflowEndpoints":     { ... }
  + "Workflow": { "Endpoints": { ... } }

  # Http.* — all flat names now under "Http:":
  - "ApiDocumentation":      { ... }
  - "ApiVersioning":         { ... }
  - "Bulkhead":              { ... }
  - "Cors":                  { ... }
  - "Cookies":               { ... }
  - "Klaro":                 { ... }
  - "HttpResilience":        { ... }
  - "Idempotency":           { ... }
  - "OutputCaching":         { ... }
  - "OutputCaching:Redis":   { ... }
  - "ResponseCompression":   { ... }
  - "SecurityHeaders":       { ... }
  + "Http": {
  +   "ApiDocumentation": { ... }, "ApiVersioning": { ... }, "Bulkhead": { ... },
  +   "Cors": { ... }, "Cookies": { ..., "Klaro": { ... } }, "Resilience": { ... },
  +   "Idempotency": { ... }, "OutputCaching": { ..., "Redis": { ... } },
  +   "ResponseCompression": { ... }, "SecurityHeaders": { ... }
  + }

  # Notifications.* — missing channel segment / wrong root:
  - "Notifications:Smtp":             { ... }
  - "Notifications:AwsSes":           { ... }
  - "AzureCommunicationServices:Email":  { ... }
  - "AzureCommunicationServices:Sms":    { ... }
  - "Notifications:AzureNotificationHubs":{ ... }
  - "Notifications:Push":             { ... }  // WebPush
  + "Notifications": {
  +   "Email": { "Smtp": { ... }, "AwsSes": { ... }, "AzureCommunicationServices": { ... } },
  +   "Sms":   { "AzureCommunicationServices": { ... } },
  +   "MobilePush": { "AzureNotificationHubs": { ... } },
  +   "WebPush": { ... }
  + }

  # Identity.Federated.* — admin providers and sub-options:
  - "KeycloakAdmin":              { ... }
  - "KeycloakAdmin:ClientRoleSync":{ ... }
  - "CognitoAdmin":               { ... }
  - "CognitoAdmin:ClientRoleSync": { ... }
  - "EntraIdAdmin":               { ... }
  - "EntraIdAdmin:ClientRoleSync": { ... }
  - "Identity:GoogleCloud":       { ... }
  - "Identity:UserCacheHasher":   { ... }
  - "IdentityUserCache":          { ... }
  - "IdentityFederatedNotifications": { ... }
  + "Identity": { "Federated": {
  +   "Keycloak":    { ..., "ClientRoleSync": { ... } },
  +   "Cognito":     { ..., "ClientRoleSync": { ... } },
  +   "EntraId":     { ..., "ClientRoleSync": { ... } },
  +   "GoogleCloud": { ... },
  +   "UserCacheHasher": { ... }, "UserCache": { ... },
  +   "Notifications":   { ... }
  + } }
  ```

  **Operational impact**: every Granit-using host must rewrite its `appsettings.json`, K8s/Vault projections (the `__` env-var syntax mirrors the new key), CI secrets, and ExternalSecret manifests in the same deploy. Containers should be rolled together: a mixed fleet will see whichever pods still read the old key fall back to defaults silently. The accompanying Keycloak JwtBearer fix (described next) addresses an `IDX10500` regression introduced by the previous Keycloak section split.

### Fixed

- `Granit.Persistence.EntityFrameworkCore` — `AuditedEntityInterceptor` now populates `ModifiedAt`/`ModifiedBy` on the aggregate-root hierarchy (`AuditedAggregateRoot`, `FullAuditedAggregateRoot`), not just on `AuditedEntity`. The `Entity` and `AggregateRoot` audit hierarchies are disjoint — an aggregate root is a `CreationAuditedEntity` but never an `AuditedEntity` — so the interceptor's modification branch (`entry.Entity is AuditedEntity`) silently skipped every audited aggregate: `CreatedAt`/`CreatedBy` were set on insert, but `ModifiedAt`/`ModifiedBy` stayed `null` after every update on `Invoice`, `Subscription`, `Document`, and any other `AuditedAggregateRoot`/`FullAuditedAggregateRoot`. A new `IModificationAuditedObject` marker interface (implemented by both `AuditedEntity` and `AuditedAggregateRoot`) is now the pivot, mirroring how `SoftDeleteInterceptor` already dispatches on `ISoftDeletable` across both hierarchies. Creation-field protection on update was already correct (it operates on the shared `CreationAuditedEntity` entry) and is now covered by aggregate-root tests too.
- **Breaking — `Granit.Authentication.JwtBearer.Keycloak`.** `AddGranitKeycloak()` now applies its Keycloak-specific JwtBearer overrides via `.Configure<>` instead of `.PostConfigure<>`: the framework's built-in `JwtBearerPostConfigureOptions` is what materialises the `ConfigurationManager<OpenIdConnectConfiguration>` from `Authority`, and it runs in the PostConfigure phase. With the previous registration, if a host provided only the Keycloak section (no `Authentication:*` doublon), `Authority` was still empty when the framework's PostConfigure ran → no ConfigurationManager → no JWKS fetch → every inbound token failed with `IDX10500 ("Signature validation failed. Unable to resolve SignatureValidator…")`. The companion `SectionName` move (`"Keycloak"` → `"Authentication:Keycloak"`) is documented in the "SectionName homogenization" entry above; see that section for the appsettings migration diff and the wider sweep across all Granit modules.
- `Granit.Http.SecurityHeaders` / `Granit.Http.ApiDocumentation` — new public marker `AllowsPopupAuthorizationMetadata` (in `Granit.Http.SecurityHeaders.Abstractions`). When attached to an endpoint, the security-headers middleware downgrades `Cross-Origin-Opener-Policy` to `unsafe-none` for that endpoint only; every other route keeps the strict `same-origin` baseline. `UseGranitApiDocumentation()` automatically attaches the marker on the Scalar route when `OAuth2` is configured. The default `same-origin` (and even `same-origin-allow-popups`) severs the opener↔popup window reference the moment the popup navigates to the IdP, breaking Scalar's polling-based Authorization Code flow: the popup completes, but the parent can no longer read its `window.location` to pick up the auth code and shows `"Window was closed without granting authorization."` Without this fix every CSP / RedirectUri change shipped in `[Unreleased]` still left Authorize broken on hosts that enable the strict COOP default.
- `Granit.Http.ApiDocumentation` — new `ApiDocumentationOptions.OAuth2.RedirectUri` property, propagated to Scalar via `flow.WithRedirectUri(...)`. Workaround for `Scalar.AspNetCore` 2.12.40+ where the default redirect URI changed and breaks Authorization Code popups (scalar/scalar#8165, #8187): the popup returns same-origin but on a URL Scalar no longer recognises, so the auth code is never piped back and the user sees `"Window was closed without granting authorization."` Set this to the absolute URL of the Scalar UI (e.g. `http://localhost:5000/scalar`) and make sure it is registered as a valid redirect URI on the IdP client. Leave unset only when upstream restores the previous default.
- `Granit.Http.ApiDocumentation` — `ScalarCspContributor` now whitelists `https://api.scalar.com` on `connect-src` (Scalar's Vue.js bootstrap fetches its curated-documents / search registry from `api.scalar.com/vector/registry/*` on mount and on every search keystroke). When `ApiDocumentationOptions.OAuth2` is configured, the contributor also adds the origins (scheme + host + port) of the OAuth2 `AuthorizationUrl` and `TokenUrl` to `connect-src`, allowing Scalar's Authorization Code → Token exchange (browser-side cross-origin POST to the IdP, e.g. `localhost:5000 → localhost:8080`) to complete instead of being blocked by CSP. Without this, the interactive "Authorize" button signed-in state never persisted. `script-src` now also carries `'unsafe-eval'`: the Scalar bundle evaluates code dynamically (template parsing, JSON Schema example rendering) via `eval` / `new Function`, which the previous policy blocked. The relaxation stays scoped to endpoints carrying `ScalarApiReferenceMetadata` and is dev-only by default (`EnableInProduction = false`).
- `Granit.Authentication.OpenIddict` & `Granit.OpenIddict.Server` — role claims emitted by OpenIddict.Validation under the OIDC short claim type `role` are now normalized to `ClaimTypes.Role`, restoring parity with `Granit.Authentication.JwtBearer`. Without this, `ICurrentUserService.GetRoles()` and the `PermissionChecker.AdminRoles` bypass returned empty even when `ClaimsPrincipal.IsInRole()` matched, causing 403s on permission-protected endpoints for admin-role users on resource servers using OpenIddict validation.

### Changed (framework)

- **Breaking — `Granit.Encryption.BackgroundJobs` package removed.** The re-encryption helper was not a Granit `IBackgroundJob` (no scheduling, no `[RecurringJob]`) — only an on-demand EF Core service. Its types have been folded into `Granit.Encryption.EntityFrameworkCore` and renamed for clarity: `IReEncryptionJob` → `IReEncryptionService`, `DefaultReEncryptionJob<TContext>` → `DefaultReEncryptionService<TContext>`. The registration helper `AddGranitEncryptionReEncryption<TContext>()` and its namespace (`Granit.Encryption.EntityFrameworkCore.Extensions`) are unchanged in spelling but now ship with the EF Core package. Consumers: drop the `Granit.Encryption.BackgroundJobs` PackageReference and rename the two types. Schedule re-encryption from an app-defined background job, admin endpoint, or CLI command — Granit itself does not orchestrate it.

### Added (framework)

- `Granit` / `Granit.Persistence.EntityFrameworkCore` — first-class design-time stubs for `IDesignTimeDbContextFactory` implementations. New public `NullDataFilter` (in `Granit.DataFiltering`) mirrors the existing `NullTenantContext` and reports every filter as enabled with no-op `Disable` / `Enable` scopes. New static helper `GranitDesignTime` (in `Granit.Persistence.EntityFrameworkCore`) exposes both as singletons (`GranitDesignTime.CurrentTenant`, `GranitDesignTime.DataFilter`) so design-time factories can hand them to a `DbContext` constructor and produce a model snapshot with the same named query filters as the runtime model — preventing EF Core 10's `PendingModelChangesWarning` from firing at app startup. The internal `NullCurrentTenant` previously duplicated inside `Granit.Caching` was removed in favour of the canonical `NullTenantContext.Instance`.
- `Granit.Authentication` — new base package hosting cross-scheme authentication primitives. Ships a generic, options-driven `RoleClaimNormalizationTransformation` (`IClaimsTransformation`) that copies values from configurable source claim types (default: OIDC short `role`) onto `ClaimTypes.Role` for principals authenticated via configurable schemes. Registered via `AddGranitRoleClaimNormalization(o => { o.Schemes.Add(...); o.SourceClaimTypes = ...; })`. Reusable by any IdP that emits roles under non-standard claim names (OpenIddict, IdentityServer, Auth0, custom). Future home for shared `ICurrentUserService` machinery currently living in `Granit.Authentication.JwtBearer`.
- `Granit.Authentication.OpenIddict` — adds `AddGranitOpenIddictRoleClaimNormalization()` sugar that pre-configures the generic primitive with the `OpenIddict.Validation.AspNetCore` scheme. Auto-applied by `AddGranitOpenIddictAuthentication()` and `AddGranitOpenIddictServer()`.

### Security

- `Granit.Vault.{HashiCorp,Azure,Aws,GoogleCloud}` — dynamic database credentials (`IDatabaseCredentialProvider.Username` / `Password`) are now stored in `byte[]` buffers and zeroized via `CryptographicOperations.ZeroMemory` on every rotation, mitigating credential residency in process memory (CWE-522 / GDPR Art. 32). Shared helper: `Granit.Vault.Internal.ZeroizingCredentialStore`.
- `Granit.Vault.HashiCorp` — `HashiCorpSecretStore` now gates the decoded size of KV v2 `__binary` payloads before allocation, protecting the host from OOM triggered by a misconfigured or compromised vault (CWE-400 / CWE-770). New option `Vault:SecretStore:MaxBinaryPayloadBytes` (default 16 MiB).
- `Granit.Encryption` — `AesStringEncryptionProvider` raises PBKDF2-SHA256 iterations from 100 000 to 600 000 to align with OWASP 2023. **Breaking for existing encrypted data:** payloads encrypted with the previous iteration count can no longer be decrypted. No production deployments are affected at this pre-1.0 stage; re-encrypt any persisted ciphertext before upgrading.

### Added

- **Auditing — graceful drain for the async persistence worker.** On SIGTERM the `AuditingPersistenceWorker` completes the channel and drains every buffered batch before exiting, eliminating audit-trail gaps on rolling deploys (bounded by the host `ShutdownTimeout`; an ungraceful SIGKILL/OOMKilled still loses the buffer — use `Strict` for hard durability). The worker is also a no-op in `Strict` mode instead of parking an idle channel reader.
- **Auditing — `AddGranitAuditingChannelHealthCheck()` on `IHealthChecksBuilder`** (`Granit.Auditing.Endpoints`) — reports `Degraded` at ≥80% channel fill and `Unhealthy` when the bounded channel is saturated (producers blocked on backpressure), letting Kubernetes readiness probes detect a stuck audit persister. Reports `Healthy` in `Strict` mode (channel unused). Opt-in, `["readiness"]` tag, 10s timeout.
- **`Granit.Auditing.BackgroundJobs`** — new package: distributed audit-log retention cleanup via the recurring-job pattern (`AuditRetentionCleanupJob`). Replaces the per-pod cleanup hosted service so a multi-replica deployment purges expired entries exactly once per schedule.
- **`Granit.Auditing.Abstractions`** — new package splitting the audit-trail contracts and domain model (`AuditEntry`, `AuditEntityChange`, `AuditCategory`, `AuditPersistenceMode`, the `IAuditing*` / `IAuditEntryPublisher` / `IAuditBatchPersister` / `IAuditChildResolver` / `IAuditEntityTypeAliasProvider` contracts, the `AuditingBatch` bus messages, the `AuditEntryPersistedEto` integration event, and `[AuditIgnore]`) out of `Granit.Auditing`. A module can now emit or read audit entries by referencing only the contracts, without pulling the runtime (persistence pipeline, background workers, query/export definitions, DI wiring). Moved types keep their `Granit.Auditing[.Domain|.Messages|.Events]` namespaces, so this is **not** a breaking change — consumers change only their `<ProjectReference>`, never their `using`s. The `Granit.Auditing` runtime now references `Granit.Auditing.Abstractions`.
- Initialisation du repository granit-dotnet
- Structure solution .NET 10 avec Central Package Management
- Projets : Abstractions, Security, Persistence, Vault, Observability
- Projets de tests associés
- CLAUDE.md, CONTRIBUTING.md, CI/CD pipeline
- `Granit.Caching` — abstraction `ICacheService<T>` + fournisseur Memory, protection stampede (double-check locking + SemaphoreSlim dans IMemoryCache), chiffrement AES-256-CBC opt-in par type via `[CacheEncrypted]`
- `Granit.Caching.StackExchangeRedis` — fournisseur Redis (StackExchange.Redis), activation `AesCacheValueEncryptor` automatique si `EncryptValues = true`
- `Granit.Caching.Hybrid` — fournisseur HybridCache L1+L2 pour Kubernetes multi-pods, `LocalCacheExpiration ≤ 60 s` pour borner la fenêtre de données obsolètes
- `Granit.Encryption` — `IStringEncryptionService` avec provider AES-256-CBC (`AesStringEncryptionProvider`) ; extensible via Vault Transit Engine
- `Granit.Localization` — localisation JSON modulaire par assembly (`EmbeddedResource`), héritage inter-modules (`[InheritResource]`), culture fallback natif, compatible `IStringLocalizer<T>`
- `Granit.Settings` — paramètres dynamiques avec résolution en cascade `User (U) → Tenant (T) → Global (G) → Configuration (C) → Default (D)`, cache intégré, `ISettingManager` / `ISettingProvider`, `InMemorySettingStore`
- `Granit.Vault` — `ISecretStore` : lecture unifiée de secrets arbitraires (certificats mTLS, clés de signature, credentials SMTP, API keys) avec `SecretDescriptor` (payload texte XOR binaire, versioning, `ExpiresOn`, `ContentType`, tags). Implémentations : HashiCorp KV v2, Azure Key Vault, AWS Secrets Manager, GCP Secret Manager. Cache L1 FusionCache **opt-in** (`Vault:SecretStore:CacheSeconds = 0` par défaut, sécurité-par-défaut). Hiérarchie d'exceptions dédiée (`SecretNotFoundException`, `SecretAccessDeniedException`, `SecretVaultTransientException`, `SecretVaultConfigurationException`) permettant au consommateur d'appliquer sa propre politique de retry. Métriques `granit.vault.secret.read` / `cache_hit` émises uniquement au niveau décorateur (pas de double-comptage). `TryGetSecretAsync` via default interface method : `null` uniquement sur not-found ; 403 / 5xx / config bullent pour éviter de masquer des pannes infra.

### Changed

- `Granit.Users` — module abstrait (interface `ICurrentUserService` uniquement) ; implémentation JWT Bearer déplacée dans `Granit.Authentication.JwtBearer`
- `Granit.Authentication.JwtBearer` — nouveau package : JWT Bearer générique (section `"Authentication"`), `CurrentUserService`, policy `Authenticated`
- `Granit.Authentication.JwtBearer.Keycloak` — nouveau package : extras Keycloak (`KeycloakClaimsTransformation`, `PostConfigure` JWT, policy `Admin`)
- `Granit.MultiTenancy` — découplé de `` (module autonome) ; `AuditedEntityInterceptor` injecte `TenantId` sur les entités `IMultiTenant`
- `Granit.Persistence` — hiérarchie de domaine : `Entity` → `CreationAuditedEntity` → `AuditedEntity` ; `ModelBuilderExtensions` applique les filtres multi-tenant et soft-delete
- `Granit.Vault` — `VaultClientFactory` utilise `IStringLocalizer<VaultLocalizationResource>` pour les messages d'erreur localisés (FR/EN)

---

## Procédure de mise à jour

### Quand mettre à jour ?

- **Toujours** lors d'une MR vers `main`
- **Jamais** lors d'un commit sur une branche feature

### Comment ?

1. Ajouter entrée dans `[Unreleased]` lors de la MR
2. Créer release lors du merge vers `main` (déplacer [Unreleased] vers nouvelle version)
3. Taguer la release : `git tag -a v0.1.0 -m "Release 0.1.0" && git push origin v0.1.0`

### Format des entrées

```markdown
### Added

- Courte description du changement (#issue ou !MR)

### Fixed

- Bug: description précise du bug corrigé (#issue)
```

### Catégories

- **Added** : Nouvelles fonctionnalités
- **Changed** : Changements dans les fonctionnalités existantes
- **Deprecated** : Fonctionnalités bientôt supprimées
- **Removed** : Fonctionnalités supprimées
- **Fixed** : Corrections de bugs
- **Security** : Corrections de vulnérabilités
