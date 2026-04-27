---
title: "Configuration Keys \u2014 All Settings Reference"
description: Complete reference of all appsettings sections and Options classes across Granit packages
sidebar:
  label: Configuration Keys
  order: 30
---

This page lists every configuration section and `Options` class provided by
Granit packages. Developers should use it as the canonical lookup when adding or
overriding configuration keys.

## Convention

All Granit configuration sections live under a **flat key** that matches the
module name. Most classes declare a `public const string SectionName` used in
the binding call. Nested sections use the colon separator
(e.g. `Cache:Redis`, `Notifications:Email`).

Typical binding in a module:

```csharp
services.Configure<ObservabilityOptions>(
    configuration.GetSection(ObservabilityOptions.SectionName));
```

## Configuration layering

The .NET configuration system merges sources in order of precedence (last wins):

1. `appsettings.json` -- base defaults, committed to the repository.
2. `appsettings.{ASPNETCORE_ENVIRONMENT}.json` -- environment overrides
   (Development, Staging, Production).
3. Environment variables -- deployed via Kubernetes ConfigMaps or container
   orchestrators.
4. HashiCorp Vault (via `Granit.Vault`) -- secrets and dynamic credentials
   injected at startup.

### Overriding via environment variables

.NET maps configuration keys to environment variables using the **double
underscore** (`__`) separator in place of `:`:

| appsettings path | Environment variable |
|---|---|
| `Cache:Redis:Configuration` | `Cache__Redis__Configuration` |
| `Observability:OtlpEndpoint` | `Observability__OtlpEndpoint` |
| `Notifications:Email:Provider` | `Notifications__Email__Provider` |
| `BlobStorage:ServiceUrl` | `BlobStorage__ServiceUrl` |

For arrays, use the index as a key segment:

```bash
Wolverine__RetryDelays__0=00:00:05
Wolverine__RetryDelays__1=00:00:30
```

---

## Core and utilities

### Timing -- `ClockOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | *(none -- configured in code)* | |
| **Package** | -- | `Granit.Timing` | |
| `DefaultTimezone` | `string?` | `null` | IANA timezone (e.g. `Europe/Brussels`). `null` = no conversion, dates remain UTC. |

### GUIDs -- `GuidGeneratorOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | *(configured in code)* | |
| **Package** | -- | `Granit.Guids` | |
| `DefaultSequentialGuidType` | `SequentialGuidType?` | `null` | `null` = `SequentialAsString` (PostgreSQL-optimized). |

---

## Security and authentication

### Authentication (JWT Bearer) -- `JwtBearerAuthOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Authentication` | |
| **Package** | -- | `Granit.Authentication.JwtBearer` | |
| `Authority` | `string` | `""` | OIDC authority URL. |
| `Audience` | `string` | `""` | Expected JWT audience. |
| `RequireHttpsMetadata` | `bool` | `true` | Require HTTPS for OIDC metadata. |
| `NameClaimType` | `string` | `"sub"` | Claim mapped to `IIdentity.Name`. |
| `BackChannelLogout:Enabled` | `bool` | `false` | Enable OIDC back-channel logout. |
| `BackChannelLogout:EndpointPath` | `string` | `"/auth/back-channel-logout"` | Logout endpoint route. |
| `BackChannelLogout:SessionRevocationTtl` | `TimeSpan` | `01:00:00` | How long revoked session IDs stay in cache. |

### Keycloak authentication -- `KeycloakOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Keycloak` | |
| **Package** | -- | `Granit.Authentication.JwtBearer.Keycloak` | |
| `Authority` | `string` | `""` | OIDC authority URL. |
| `ClientId` | `string` | `""` | Keycloak client ID. |
| `ClientSecret` | `string` | `""` | Client secret (load from Vault). |
| `RequireHttpsMetadata` | `bool` | `true` | Require HTTPS for OIDC metadata. |
| `Audience` | `string?` | `null` | Expected audience. Defaults to `ClientId`. |
| `AdminRole` | `string` | `"admin"` | Keycloak realm role for admins. |
| `RoleClaimsSource` | `string` | `"realm_access"` | `"realm_access"` or `"resource_access"`. |

### Entra ID authentication -- `EntraIdOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `EntraId` | |
| **Package** | -- | `Granit.Authentication.JwtBearer.EntraId` | |
| `Instance` | `string` | `"https://login.microsoftonline.com/"` | Azure AD instance URL. |
| `TenantId` | `string` | `""` | Azure AD tenant ID. |
| `ClientId` | `string` | `""` | App registration client ID. |
| `RequireHttpsMetadata` | `bool` | `true` | Require HTTPS for OIDC metadata. |
| `AdminRole` | `string` | `"admin"` | Admin App Role name. |

### Cognito authentication -- `CognitoOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Cognito` | |
| **Package** | -- | `Granit.Authentication.JwtBearer.Cognito` | |
| `UserPoolId` | `string` | `""` | Cognito User Pool ID. |
| `ClientId` | `string` | `""` | Cognito app client ID. |
| `Region` | `string` | `""` | AWS region (e.g. `eu-west-1`). |

### API key authentication -- `ApiKeyOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | *(configured via authentication scheme)* | |
| **Package** | -- | `Granit.Authentication.ApiKeys` | |
| `CacheDuration` | `TimeSpan` | `00:05:00` | Cache TTL for API key lookups. `TimeSpan.Zero` to disable. |
| `TrackLastUsed` | `bool` | `true` | Update `LastUsedAt` on each request. |

### API key endpoints -- `ApiKeysEndpointsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `ApiKeysEndpoints` | |
| **Package** | -- | `Granit.Authentication.ApiKeys.Endpoints` | |
| `RoutePrefix` | `string` | `"api-keys"` | Route prefix. |
| `TagName` | `string` | `"API Keys"` | OpenAPI tag. |
| `RequiredRole` | `string` | `"granit-apikeys-admin"` | Fallback authorization role. |
| `AllowedEnvironments` | `string[]` | `["live","test","dev"]` | Allowed environments for key creation. |

### Authorization -- `GranitAuthorizationOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Authorization` | |
| **Package** | -- | `Granit.Authorization` | |
| `AdminRoles` | `string[]` | `["admin"]` | Roles that bypass all permission checks. |
| `CacheDuration` | `TimeSpan` | `00:05:00` | Permission check cache TTL. |
| `AlwaysAllow` | `bool` | `false` | Skip permission checks entirely (dev only). |

### Authorization endpoints -- `AuthorizationEndpointsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `AuthorizationEndpoints` | |
| **Package** | -- | `Granit.Authorization.Endpoints` | |
| `RoutePrefix` | `string` | `"auth"` | Route prefix. |
| `TagName` | `string` | `"Authorization"` | OpenAPI tag. |

### Vault -- `HashiCorpVaultOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Vault` | |
| **Package** | -- | `Granit.Vault.HashiCorp` | |
| `Address` | `string` | `""` | Vault server URL. |
| `AuthMethod` | `string` | `"Kubernetes"` | `"Kubernetes"` or `"Token"`. |
| `Token` | `string?` | `null` | Vault token (dev only). |
| `KubernetesRole` | `string` | `"my-backend"` | K8s auth role. |
| `KubernetesTokenPath` | `string` | `/var/run/secrets/.../token` | Path to K8s service account JWT. |
| `DatabaseMountPoint` | `string` | `"database"` | Database engine mount point. |
| `DatabaseRoleName` | `string` | `"readwrite"` | Dynamic credential role. |
| `TransitMountPoint` | `string` | `"transit"` | Transit engine mount point. |
| `LeaseRenewalThreshold` | `double` | `0.75` | Lease renewal at 75% of TTL. |

### Encryption -- `StringEncryptionOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Encryption` | |
| **Package** | -- | `Granit.Encryption` | |
| `PassPhrase` | `string` | `""` | AES key derivation passphrase (from Vault). |
| `KeySize` | `int` | `256` | AES key size in bits. |
| `ProviderName` | `string` | `"Aes"` | `"Aes"`, `"Vault"`, or `"AzureKeyVault"`. |
| `VaultKeyName` | `string` | `"string-encryption"` | Transit key name (when provider is `Vault`). |

### Azure Key Vault -- `AzureKeyVaultOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Vault:Azure` | |
| **Package** | -- | `Granit.Vault.Azure` | |
| `VaultUri` | `string` | `""` | Azure Key Vault URI (e.g. `https://my-vault.vault.azure.net/`). |
| `EncryptionKeyName` | `string` | `"string-encryption"` | Key name for encrypt/decrypt operations. |
| `EncryptionAlgorithm` | `string` | `"RSA-OAEP-256"` | Algorithm: `RSA-OAEP-256` or `RSA-OAEP`. |
| `DatabaseSecretName` | `string?` | `null` | Secret name for DB credentials. Omit to disable credential rotation. |
| `RotationCheckIntervalMinutes` | `int` | `5` | Secret version polling interval (minutes). |
| `TimeoutSeconds` | `int` | `30` | Azure SDK operation timeout. |

### Privacy -- `GranitPrivacyOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Privacy` | |
| **Package** | -- | `Granit.Privacy` | |
| `ExportTimeoutMinutes` | `int` | `5` | GDPR export saga timeout. |
| `ExportMaxSizeMb` | `int` | `100` | Maximum export archive size (MB). |

---

## Identity

### Keycloak Admin API -- `KeycloakAdminOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `KeycloakAdmin` | |
| **Package** | -- | `Granit.Identity.Federated.Keycloak` | |
| `BaseUrl` | `string` | `""` | Keycloak server base URL. |
| `Realm` | `string` | `""` | Realm name. |
| `ClientId` | `string` | `""` | Service account client ID. |
| `ClientSecret` | `string` | `""` | Service account client secret (from Vault). |
| `UseTokenExchangeForDeviceActivity` | `bool` | `false` | Use token exchange for device-level session info. |
| `TimeoutSeconds` | `int` | `30` | HTTP request timeout. |
| `DirectAccessClientId` | `string?` | `null` | Public client for credential verification. |

### Entra ID Admin API -- `EntraIdAdminOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `EntraIdAdmin` | |
| **Package** | -- | `Granit.Identity.Federated.EntraId` | |
| `TenantId` | `string` | `""` | Azure AD tenant ID. |
| `ClientId` | `string` | `""` | Service principal client ID. |
| `ClientSecret` | `string` | `""` | Service principal secret (from Vault). |
| `ServicePrincipalObjectId` | `string` | `""` | Enterprise app Object ID for App Role ops. |
| `DefaultDomain` | `string?` | `null` | Domain for `userPrincipalName` construction. |
| `TimeoutSeconds` | `int` | `30` | Microsoft Graph request timeout. |
| `GraphBaseUrl` | `string` | `"https://graph.microsoft.com"` | Graph API base URL. |
| `RopcClientId` | `string?` | `null` | Public client for ROPC credential verification. |

### Cognito Admin API -- `CognitoAdminOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `CognitoAdmin` | |
| **Package** | -- | `Granit.Identity.Federated.Cognito` | |
| `UserPoolId` | `string` | `""` | Cognito User Pool ID. |
| `Region` | `string` | `""` | AWS region (e.g. `eu-west-1`). |
| `AccessKeyId` | `string?` | `null` | AWS access key (optional — uses default credential chain if omitted). |
| `SecretAccessKey` | `string?` | `null` | AWS secret key (from Vault). |
| `TimeoutSeconds` | `int` | `30` | HTTP request timeout. |

### User cache -- `UserCacheOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `IdentityUserCache` | |
| **Package** | -- | `Granit.Identity.Federated.EntityFrameworkCore` | |
| `StalenessThreshold` | `TimeSpan` | `1.00:00:00` | Duration before a cached user is stale. |
| `EnableLoginTimeSync` | `bool` | `true` | Auto-sync user from JWT on each request. |
| `IncrementalSyncBatchSize` | `int` | `50` | Max stale entries per incremental sync batch. |

### Identity endpoints -- `IdentityEndpointsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `IdentityEndpoints` | |
| **Package** | -- | `Granit.Identity.Endpoints` | |
| `RoutePrefix` | `string` | `"identity/users"` | Route prefix. |
| `TagName` | `string` | `"Identity User Cache"` | OpenAPI tag. |
| `RequiredRole` | `string` | `"granit-identity-admin"` | Fallback authorization role. |

### Identity webhook -- `IdentityWebhookOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `IdentityWebhook` | |
| **Package** | -- | `Granit.Identity.Endpoints` | |
| `Secret` | `string` | `""` | **Required.** HMAC-SHA256 shared secret. Webhook rejects all requests when empty (fail-closed). |
| `SignatureHeaderName` | `string` | `"X-Webhook-Signature"` | HTTP header carrying the signature. |

---

## Data and persistence

### Caching -- `CachingOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Cache` | |
| **Package** | -- | `Granit.Caching` | |
| `KeyPrefix` | `string` | `"dd"` | Prefix for all cache keys. |
| `DefaultAbsoluteExpirationRelativeToNow` | `TimeSpan?` | `01:00:00` | Default absolute expiration. |
| `DefaultSlidingExpiration` | `TimeSpan?` | `00:20:00` | Default sliding expiration. |
| `EncryptValues` | `bool` | `false` | Enable AES-256 encryption for all cached values. |

### Cache encryption -- `CacheEncryptionOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Cache:Encryption` | |
| **Package** | -- | `Granit.Caching` | |
| `Key` | `string?` | `null` | AES-256 key (base64, 32 bytes). From Vault in production. |

### FusionCache -- `FusionCacheOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Cache:FusionCache` | |
| **Package** | -- | `Granit.Caching` | |
| `DefaultEntryOptions:Duration` | `TimeSpan` | `00:05:00` | Default cache entry duration. |

### Redis cache -- `RedisCachingOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Cache:Redis` | |
| **Package** | -- | `Granit.Caching.StackExchangeRedis` | |
| `IsEnabled` | `bool` | `true` | Enable Redis provider (`false` = Memory fallback). |
| `Configuration` | `string` | `"localhost:6379"` | StackExchange.Redis connection string. |
| `InstanceName` | `string` | `"dd:"` | Redis key prefix for app isolation. |

### Multi-tenancy -- `MultiTenancyOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `MultiTenancy` | |
| **Package** | -- | `Granit.MultiTenancy` | |
| `IsEnabled` | `bool` | `true` | Enable tenant resolution middleware. |
| `TenantIdClaimType` | `string` | `"tenant_id"` | JWT claim for tenant ID. |
| `TenantIdHeaderName` | `string` | `"X-Tenant-Id"` | HTTP header for tenant ID. |
| `HeaderTrustMode` | `TenantHeaderTrustMode` | `Unrestricted` | `Unrestricted` or `CrossValidate` (match header against JWT claim). |

### Tenant isolation -- `TenantIsolationOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `TenantIsolation` | |
| **Package** | -- | `Granit.Persistence` | |
| `Strategy` | `TenantIsolationStrategy` | `SharedDatabase` | `SharedDatabase`, `TenantPerSchema`, or `TenantPerDatabase`. |

### Tenant schema -- `TenantSchemaOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `TenantSchema` | |
| **Package** | -- | `Granit.Persistence` | |
| `NamingConvention` | `TenantSchemaNamingConvention` | `TenantId` | `TenantId`, `TenantName`, or `Custom`. |
| `Prefix` | `string` | `"tenant_"` | Schema name prefix. Must match `^[a-z][a-z0-9_]*$` (validated at startup). |

### Data migrations -- `MigrationStartupOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `GranitMigrations` | |
| **Package** | -- | `Granit.Persistence.Migrations` | |
| `DefaultBatchSize` | `int` | `500` | Rows per batch when resuming pending cycles. |
| `BatchExecutionTimeout` | `TimeSpan` | `00:05:00` | Timeout per migration batch. |

### Settings -- `SettingsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Settings` | |
| **Package** | -- | `Granit.Settings` | |
| `CacheExpiration` | `TimeSpan` | `00:30:00` | Settings cache entry lifetime. |

### Settings endpoints -- `SettingsEndpointsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | *(configured in code)* | |
| **Package** | -- | `Granit.Settings.Endpoints` | |
| `UserRoutePrefix` | `string` | `"settings/user"` | User-scoped settings route. |
| `GlobalRoutePrefix` | `string` | `"settings/global"` | Global settings route. |
| `TenantRoutePrefix` | `string` | `"settings/tenant"` | Tenant-scoped settings route. |
| `TagName` | `string` | `"Settings"` | OpenAPI tag. |

### Reference data -- `ReferenceDataOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `ReferenceData` | |
| **Package** | -- | `Granit.ReferenceData` | |
| `CacheTimeToLive` | `TimeSpan` | `01:00:00` | In-memory cache TTL for reference data. |

### Reference data extension -- `ReferenceDataExtensionOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | *(configured in code via fluent API)* | |
| **Package** | -- | `Granit.ReferenceData` | |
| `TableName` | `string` | `"ref_data"` | Database table name for the dynamic type. |
| `IsHierarchical` | `bool` | `false` | Enable parent-child hierarchy via `ParentCode`. |
| `PropertyMappings` | `List<...>` | `[]` | Dynamic SQL column mappings via `MapProperty<T>()`. |

### Reference data endpoints -- `ReferenceDataEndpointsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | *(configured in code)* | |
| **Package** | -- | `Granit.ReferenceData.Endpoints` | |
| `RoutePrefix` | `string` | `"reference-data"` | Route prefix. |
| `TagName` | `string` | `"Reference Data"` | OpenAPI tag. |
| `AdminPolicyName` | `string?` | `"ReferenceData.Admin"` | Admin authorization policy. `null` = no auth. |
| `RequiredRole` | `string` | `"granit-reference-data-admin"` | Fallback admin role. |

### QueryEngine -- `QueryEngineOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `QueryEngine` | |
| **Package** | -- | `Granit.QueryEngine` | |
| `DefaultPageSize` | `int` | `20` | Default page size for paginated queries. |
| `MaxPageSize` | `int` | `100` | Maximum allowed page size. |
| `MaxStreamSize` | `int` | `100000` | Maximum items returned by streaming queries. |
| `MaxGroupCount` | `int` | `1000` | Maximum groups returned by grouped queries. Prevents OOM from high-cardinality fields. |
| `MaxSavedViewsPerUser` | `int` | `100` | Maximum saved views per user per entity type. Prevents storage exhaustion. |
| `CursorHmacKey` | `string?` | `null` | Base64-encoded HMAC-SHA256 key for cursor signing. When `null`, cursors are unsigned. |

### QueryEngine AI -- `QueryEngineAIOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `AI:QueryEngine` | |
| **Package** | -- | `Granit.QueryEngine.AI` | |
| `WorkspaceName` | `string` | `"default"` | AI workspace for NLQ translation. |
| `TimeoutSeconds` | `int` | `5` | Timeout for LLM calls. Validated at startup (must be > 0). |

---

## API and web

### API versioning -- `GranitApiVersioningOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `ApiVersioning` | |
| **Package** | -- | `Granit.Http.ApiVersioning` | |
| `DefaultMajorVersion` | `int` | `1` | Default API version when client omits it. |
| `ReportApiVersions` | `bool` | `true` | Include version headers in responses. |

### API documentation -- `ApiDocumentationOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `ApiDocumentation` | |
| **Package** | -- | `Granit.Http.ApiDocumentation` | |
| `MajorVersions` | `int[]` | `[1]` | API versions to generate OpenAPI docs for. |
| `Title` | `string` | `"API"` | API title in Scalar UI. |
| `Description` | `string?` | `null` | Markdown description in OpenAPI info. |
| `PartyEmail` | `string?` | `null` | Party email in OpenAPI info. |
| `LogoUrl` | `string?` | `null` | Logo image URL for Scalar UI. |
| `FaviconUrl` | `string?` | `null` | Favicon URL for Scalar page. |
| `EnableInProduction` | `bool` | `false` | Expose docs in Production. |
| `EnableTenantHeader` | `bool` | `false` | Document tenant header on endpoints. |
| `TenantHeaderName` | `string` | `"X-Tenant-Id"` | Tenant header name. |
| `AuthorizationPolicy` | `string?` | `null` | Policy for doc endpoints. `""` = anonymous. |
| `OAuth2:AuthorizationUrl` | `string?` | `null` | OAuth2 authorization endpoint. |
| `OAuth2:TokenUrl` | `string?` | `null` | OAuth2 token endpoint. |
| `OAuth2:ClientId` | `string?` | `null` | Public OAuth2 client ID (PKCE). |
| `OAuth2:EnablePkce` | `bool` | `true` | Enable PKCE with S256. |
| `OAuth2:Scopes` | `string[]` | `["openid"]` | OAuth2 scopes to request. |

### Exception handling -- `ExceptionHandlingOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | *(configured in code)* | |
| **Package** | -- | `Granit.Http.ExceptionHandling` | |
| `ExposeInternalErrorDetails` | `bool` | `false` | Show internal error messages in ProblemDetails. Never `true` in production (ISO 27001). |

### CORS -- `GranitCorsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Cors` | |
| **Package** | -- | `Granit.Http.Cors` | |
| `AllowedOrigins` | `string[]` | `[]` | Allowed CORS origins. Wildcard `*` forbidden outside Development (ISO 27001). |
| `AllowCredentials` | `bool` | `false` | Include `Access-Control-Allow-Credentials`. |

### Cookies -- `GranitCookiesOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Cookies` | |
| **Package** | -- | `Granit.Http.Cookies` | |
| `ThrowOnUnregistered` | `bool` | `true` | Fail-fast on unregistered cookies. |
| `DefaultRetentionDays` | `int` | `365` | Default cookie retention period. |
| `ThirdPartyServices` | `array` | `[]` | Third-party services for CMP setup (see below). |

Each entry in `ThirdPartyServices`:

| Key | Type | Description |
|---|---|---|
| `Name` | `string` | Service identifier (e.g. `"matomo"`). |
| `Category` | `CookieCategory` | GDPR consent category. |
| `CookiePatterns` | `string[]` | Regex patterns matching service cookies. |

### Cookies -- Klaro CMP -- `KlaroOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Klaro` | |
| **Package** | -- | `Granit.Http.Cookies.Klaro` | |
| `CookieName` | `string` | `"klaro"` | Klaro consent cookie name. |

### Cookie consent endpoints -- `CookieConsentEndpointsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | *(configured in code)* | |
| **Package** | -- | `Granit.Http.Cookies.Endpoints` | |
| `RoutePrefix` | `string` | `"cookies"` | Route prefix. |
| `TagName` | `string` | `"Cookies"` | OpenAPI tag. |

### Idempotency -- `IdempotencyOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Idempotency` | |
| **Package** | -- | `Granit.Http.Idempotency` | |
| `HeaderName` | `string` | `"Idempotency-Key"` | HTTP header name. |
| `KeyPrefix` | `string` | `"idp"` | Redis key prefix. |
| `CompletedTtl` | `TimeSpan` | `1.00:00:00` | TTL for completed entries. |
| `InProgressTtl` | `TimeSpan` | `00:00:30` | TTL for in-progress lock. |
| `ExecutionTimeout` | `TimeSpan` | `00:00:25` | Max downstream handler execution time. |
| `MaxBodySizeBytes` | `int` | `1048576` | Max request body size to hash (1 MiB). |

### Rate limiting -- `GranitRateLimitingOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `RateLimiting` | |
| **Package** | -- | `Granit.RateLimiting` | |
| `Enabled` | `bool` | `true` | Enable rate limiting. |
| `KeyPrefix` | `string` | `"rl"` | Redis key prefix for counters. |
| `FallbackOnCounterStoreFailure` | `CounterStoreFailureBehavior` | `Deny` | Behavior when Redis is unavailable. |
| `BypassRoles` | `string[]` | `[]` | Roles exempt from rate limiting. |
| `UseFeatureBasedQuotas` | `bool` | `false` | Use `Granit.Features` for plan-based quotas. |
| `Policies` | `Dictionary` | `{}` | Named rate limit policies (see below). |

Each entry in `Policies`:

| Key | Type | Default | Description |
|---|---|---|---|
| `Algorithm` | `RateLimitAlgorithm` | `SlidingWindow` | `SlidingWindow`, `FixedWindow`, or `TokenBucket`. |
| `PartitionBy` | `RateLimitPartition` | `Tenant` | Key partition: `Tenant`, `TenantAndIp`, `Ip`, `User`, `TenantAndUser`. |
| `PermitLimit` | `int` | `1000` | Max permits per window. |
| `Window` | `TimeSpan` | `00:01:00` | Time window for sliding/fixed algorithms. |
| `SegmentsPerWindow` | `int` | `6` | Segments per sliding window (1--60). |
| `TokenLimit` | `int` | `50` | Max tokens for `TokenBucket`. |
| `TokensPerPeriod` | `int` | `10` | Tokens added per replenishment. |
| `ReplenishmentPeriod` | `TimeSpan` | `00:00:10` | Interval between replenishments. |
| `FeatureName` | `string?` | `null` | Feature name override for quota resolution. |

### Bulkhead isolation -- `GranitBulkheadOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Bulkhead` | |
| **Package** | -- | `Granit.Http.Bulkhead` | |
| `Enabled` | `bool` | `true` | Enable bulkhead isolation. |
| `BypassRoles` | `string[]` | `[]` | Roles exempt from bulkhead checks. |
| `UseFeatureBasedQuotas` | `bool` | `false` | Use `Granit.Features` for dynamic limits. |
| `IdleTimeout` | `TimeSpan` | `00:30:00` | TTL for idle limiters before eviction. |
| `CleanupInterval` | `TimeSpan` | `00:05:00` | Interval between cleanup sweeps. |
| `Policies` | `Dictionary` | `{}` | Named bulkhead policies (see below). |

Each entry in `Policies`:

| Key | Type | Default | Description |
|---|---|---|---|
| `PermitLimit` | `int` | `10` | Max concurrent operations per tenant (1--10,000). |
| `QueueLimit` | `int` | `0` | Max queued operations. `0` = reject immediately. |
| `QueueTimeout` | `TimeSpan` | `00:00:30` | Max time in queue before rejection. |
| `FeatureName` | `string?` | `null` | Feature name override for dynamic resolution. |

---

## Messaging and events

### Wolverine (core) -- `WolverineMessagingOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Wolverine` | |
| **Package** | -- | `Granit.Wolverine` | |
| `RetryDelays` | `TimeSpan[]` | `[00:00:05, 00:00:30, 00:05:00]` | Cooldown delays between retry attempts. |
| `MaxRetryAttempts` | `int` | `3` | Maximum retry attempts. |

### Wolverine PostgreSQL -- `WolverinePostgresqlOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `WolverinePostgresql` | |
| **Package** | -- | `Granit.Wolverine.Postgresql` | |
| `TransportConnectionString` | `string` | `""` | PostgreSQL connection string for outbox tables. |
| `TransactionMode` | `TransactionMiddlewareMode` | `Eager` | `Eager` (ISO 27001-recommended) or `Lightweight`. |

### Wolverine SQL Server -- `WolverineSqlServerOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `WolverineSqlServer` | |
| **Package** | -- | `Granit.Wolverine.SqlServer` | |
| `TransportConnectionString` | `string` | `""` | SQL Server connection string for outbox tables. |
| `TransactionMode` | `TransactionMiddlewareMode` | `Eager` | `Eager` (ISO 27001-recommended) or `Lightweight`. |

### Webhooks -- `WebhooksOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Webhooks` | |
| **Package** | -- | `Granit.Webhooks` | |
| `HttpTimeoutSeconds` | `int` | `10` | HTTP delivery timeout (5--120). |
| `MaxParallelDeliveries` | `int` | `20` | Parallel deliveries on the local queue (1--100). |
| `StorePayload` | `bool` | `false` | Persist delivery payloads (GDPR: validate with DPO). |

### Notifications (engine) -- `NotificationsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications` | |
| **Package** | -- | `Granit.Notifications` | |
| `MaxParallelDeliveries` | `int` | `8` | Max parallel delivery messages. |

### Notification endpoints -- `NotificationEndpointsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | *(configured in code)* | |
| **Package** | -- | `Granit.Notifications.Endpoints` | |
| `RoutePrefix` | `string` | `"notifications"` | Route prefix. |
| `TagName` | `string` | `"Notifications"` | OpenAPI tag. |

### Email channel -- `EmailChannelOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:Email` | |
| **Package** | -- | `Granit.Notifications.Email` | |
| `Provider` | `string` | `"Smtp"` | Keyed service provider (`"Smtp"`, `"Brevo"`, `"AzureCommunicationServices"`, `"Scaleway"`, `"SendGrid"`). |
| `SenderAddress` | `string` | `""` | Default sender email. |
| `SenderName` | `string` | `""` | Default sender display name. |

### SMTP -- `SmtpOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:Smtp` | |
| **Package** | -- | `Granit.Notifications.Email.Smtp` | |
| `Host` | `string` | `"localhost"` | SMTP server hostname. |
| `Port` | `int` | `587` | SMTP server port. |
| `UseSsl` | `bool` | `true` | Use SSL/TLS. |
| `Username` | `string?` | `null` | SMTP username. |
| `Password` | `string?` | `null` | SMTP password (from Vault). |
| `TimeoutSeconds` | `int` | `30` | Connection/send timeout. |

### Brevo -- `BrevoOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:Brevo` | |
| **Package** | -- | `Granit.Notifications.Brevo` | |
| `ApiKey` | `string` | `""` | Brevo API key (from Vault). |
| `DefaultSenderEmail` | `string` | `""` | Default sender email. |
| `DefaultSenderName` | `string` | `""` | Default sender name. |
| `DefaultSmsSenderId` | `string` | `""` | Default SMS sender ID. |
| `BaseUrl` | `string` | `"https://api.brevo.com/v3"` | Brevo API base URL. |
| `TimeoutSeconds` | `int` | `30` | HTTP request timeout. |

### Scaleway TEM -- `ScalewayEmailOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:Email:Scaleway` | |
| **Package** | -- | `Granit.Notifications.Email.Scaleway` | |
| `SecretKey` | `string` | `""` | Scaleway API secret key (from Vault). |
| `ProjectId` | `string` | `""` | Scaleway project ID. |
| `DefaultSenderEmail` | `string` | `""` | Default sender email (must be verified in Scaleway TEM). |
| `DefaultSenderName` | `string` | `""` | Default sender display name. |
| `Region` | `string` | `"fr-par"` | Scaleway region. |
| `BaseUrl` | `string` | `"https://api.scaleway.com"` | Scaleway API base URL. |
| `TimeoutSeconds` | `int` | `30` | HTTP request timeout. |

### SendGrid -- `SendGridEmailOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:Email:SendGrid` | |
| **Package** | -- | `Granit.Notifications.Email.SendGrid` | |
| `ApiKey` | `string` | `""` | SendGrid API key (from Vault). |
| `DefaultSenderEmail` | `string` | `""` | Default sender email (must be verified in SendGrid). |
| `DefaultSenderName` | `string` | `""` | Default sender display name. |
| `SandboxMode` | `bool` | `false` | Enable SendGrid sandbox mode (no actual delivery). |
| `TimeoutSeconds` | `int` | `30` | HTTP request timeout. |

### Twilio -- `TwilioOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:Twilio` | |
| **Package** | -- | `Granit.Notifications.Twilio` | |
| `AccountSid` | `string` | `""` | Twilio Account SID. |
| `AuthToken` | `string` | `""` | Twilio Auth Token (from Vault). |
| `DefaultSmsFrom` | `string` | `""` | Default SMS sender number (E.164 format) or Messaging Service SID. |
| `DefaultWhatsAppFrom` | `string` | `""` | Default WhatsApp sender (e.g. `whatsapp:+14155238886`). |
| `MessagingServiceSid` | `string?` | `null` | Twilio Messaging Service SID (overrides `DefaultSmsFrom` when set). |
| `TimeoutSeconds` | `int` | `30` | HTTP request timeout. |

### SMS channel -- `SmsChannelOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:Sms` | |
| **Package** | -- | `Granit.Notifications.Sms` | |
| `Provider` | `string` | `""` | Keyed service provider (`"Brevo"`, `"AzureCommunicationServices"`, `"AwsSns"`, `"Twilio"`). |
| `SenderId` | `string?` | `null` | Default sender ID. |

### WhatsApp channel -- `WhatsAppChannelOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:WhatsApp` | |
| **Package** | -- | `Granit.Notifications.WhatsApp` | |
| `Provider` | `string` | `""` | Keyed service provider (`"Brevo"`, `"Twilio"`). |

### Web Push (VAPID) -- `PushChannelOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:Push` | |
| **Package** | -- | `Granit.Notifications.WebPush` | |
| `VapidSubject` | `string` | `""` | VAPID subject (`mailto:` or `https:` URL). |
| `VapidPublicKey` | `string` | `""` | VAPID public key (base64 URL-safe). |
| `VapidPrivateKey` | `string` | `""` | VAPID private key (from Vault). |

### Mobile Push -- `MobilePushChannelOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:MobilePush` | |
| **Package** | -- | `Granit.Notifications.MobilePush` | |
| `Provider` | `string` | `"GoogleFcm"` | Keyed service provider. |

### Firebase Cloud Messaging -- `GoogleFcmOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:MobilePush:GoogleFcm` | |
| **Package** | -- | `Granit.Notifications.MobilePush.GoogleFcm` | |
| `ProjectId` | `string` | `""` | Firebase project ID. |
| `ServiceAccountJson` | `string` | `""` | Service account JSON key (from Vault). |
| `BaseAddress` | `string` | `"https://fcm.googleapis.com/"` | FCM API base address. |
| `TimeoutSeconds` | `int` | `30` | Request timeout. |

### ACS Email -- `AcsEmailOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `AzureCommunicationServices:Email` | |
| **Package** | -- | `Granit.Notifications.Email.AzureCommunicationServices` | |
| `ConnectionString` | `string?` | `null` | ACS connection string. Mutually exclusive with `Endpoint`. |
| `Endpoint` | `string?` | `null` | ACS endpoint URI (uses `DefaultAzureCredential`). |
| `SenderAddress` | `string` | `""` | Sender email address (must be verified in ACS). |
| `TimeoutSeconds` | `int` | `120` | Send operation timeout (ACS emails can take time). |

### ACS SMS -- `AcsSmsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `AzureCommunicationServices:Sms` | |
| **Package** | -- | `Granit.Notifications.Sms.AzureCommunicationServices` | |
| `ConnectionString` | `string?` | `null` | ACS connection string. Mutually exclusive with `Endpoint`. |
| `Endpoint` | `string?` | `null` | ACS endpoint URI (uses `DefaultAzureCredential`). |
| `FromPhoneNumber` | `string` | `""` | Sender phone number in E.164 format (must start with `+`). |
| `TimeoutSeconds` | `int` | `30` | Send operation timeout. |

### Azure Notification Hubs -- `AzureNotificationHubsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:AzureNotificationHubs` | |
| **Package** | -- | `Granit.Notifications.MobilePush.AzureNotificationHubs` | |
| `ConnectionString` | `string` | `""` | Notification Hub connection string. |
| `HubName` | `string` | `""` | Notification Hub name. |
| `TimeoutSeconds` | `int` | `30` | Send operation timeout. |

### SignalR channel -- `SignalRChannelOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:SignalR` | |
| **Package** | -- | `Granit.Notifications.SignalR` | |
| `RedisConnectionString` | `string?` | `null` | Redis connection for SignalR backplane (multi-pod). |

### SSE channel -- `SseChannelOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:Sse` | |
| **Package** | -- | `Granit.Notifications.Sse` | |
| `HeartbeatIntervalSeconds` | `int` | `30` | Keep-alive heartbeat interval. |

### Zulip channel -- `ZulipChannelOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:Zulip` | |
| **Package** | -- | `Granit.Notifications.Zulip` | |
| `DefaultStream` | `string` | `"alerts"` | Default Zulip stream. |
| `DefaultTopic` | `string` | `"system"` | Default Zulip topic. |

### Zulip bot -- `ZulipBotOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Notifications:Zulip:Bot` | |
| **Package** | -- | `Granit.Notifications.Zulip` | |
| `BaseUrl` | `string` | `""` | Zulip server base URL. |
| `BotEmail` | `string` | `""` | Bot email address. |
| `ApiKey` | `string` | `""` | Bot API key (from Vault). |
| `TimeoutSeconds` | `int` | `30` | Request timeout. |

---

## Documents and templates

### Templating endpoints -- `TemplatingEndpointsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | *(configured in code)* | |
| **Package** | -- | `Granit.Templating.Endpoints` | |
| `RoutePrefix` | `string` | `"templates"` | Route prefix. |
| `TagName` | `string` | `"Templates"` | OpenAPI tag. |

### PDF rendering -- `PdfRenderOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `DocumentGeneration:Pdf` | |
| **Package** | -- | `Granit.DocumentGeneration.Pdf` | |
| `PaperFormat` | `string` | `"A4"` | Paper format (`A4`, `A5`, `Letter`). |
| `Landscape` | `bool` | `false` | Landscape orientation. |
| `MarginTop` | `string` | `"10mm"` | Top margin (CSS units). |
| `MarginBottom` | `string` | `"10mm"` | Bottom margin. |
| `MarginLeft` | `string` | `"10mm"` | Left margin. |
| `MarginRight` | `string` | `"10mm"` | Right margin. |
| `HeaderTemplate` | `string?` | `null` | HTML header template (PuppeteerSharp classes). |
| `FooterTemplate` | `string?` | `null` | HTML footer template. |
| `PrintBackground` | `bool` | `true` | Print background graphics. |
| `ChromiumExecutablePath` | `string?` | `null` | Custom Chromium path. `null` = PuppeteerSharp-managed. |
| `MaxConcurrentPages` | `int` | `4` | Max parallel Chromium tabs (1--32). |

---

## Data exchange (import/export)

### Import -- `ImportOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `DataExchange` | |
| **Package** | -- | `Granit.DataExchange` | |
| `DefaultMaxFileSizeMb` | `int` | `50` | Max file size (MB) unless overridden per definition. |
| `DefaultBatchSize` | `int` | `500` | Default import batch size. |
| `FuzzyMatchThreshold` | `double` | `0.8` | Minimum fuzzy matching score for mapping suggestions. |

### Export -- `ExportOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `DataExport` | |
| **Package** | -- | `Granit.DataExchange` | |
| `BackgroundThreshold` | `int` | `1000` | Row count above which export runs as a background job. |

### Data exchange endpoints -- `DataExchangeEndpointsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `DataExchangeEndpoints` | |
| **Package** | -- | `Granit.DataExchange.Endpoints` | |
| `RoutePrefix` | `string` | `"data-exchange"` | Route prefix. |
| `RequiredRole` | `string` | `"granit-data-exchange-admin"` | Fallback authorization role. |
| `TagName` | `string` | `"Data Exchange"` | OpenAPI tag. |

---

## Workflow

### Workflow endpoints -- `WorkflowEndpointsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `WorkflowEndpoints` | |
| **Package** | -- | `Granit.Workflow.Endpoints` | |
| `RoutePrefix` | `string` | `"workflow"` | Route prefix. |
| `RequiredRole` | `string` | `"granit-workflow-admin"` | Fallback authorization role. |
| `TagName` | `string` | `"Workflow"` | OpenAPI tag. |

---

## Diagnostics and observability

### Observability -- `ObservabilityOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `Observability` | |
| **Package** | -- | `Granit.Observability` | |
| `ServiceName` | `string` | `"unknown-service"` | OTEL service name. |
| `ServiceVersion` | `string` | `"0.0.0"` | Service version. |
| `OtlpEndpoint` | `string` | `"http://localhost:4317"` | OTLP gRPC endpoint. |
| `ServiceNamespace` | `string` | `"my-company"` | OTEL service namespace. |
| `Environment` | `string` | `"development"` | Deployment environment. |
| `EnableTracing` | `bool` | `true` | Enable trace export. |
| `EnableMetrics` | `bool` | `true` | Enable metrics export. |

### Diagnostics -- `DiagnosticsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | *(configured in code)* | |
| **Package** | -- | `Granit.Diagnostics` | |
| `LivenessPath` | `string` | `"/health/live"` | Liveness probe path. |
| `ReadinessPath` | `string` | `"/health/ready"` | Readiness probe path. |
| `StartupPath` | `string` | `"/health/startup"` | Startup probe path. |
| `DefaultCacheDuration` | `TimeSpan` | `00:00:10` | Health check cache duration. |

### Timeline endpoints -- `TimelineEndpointsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `TimelineEndpoints` | |
| **Package** | -- | `Granit.Timeline.Endpoints` | |
| `RoutePrefix` | `string` | `"timeline"` | Route prefix. |
| `RequiredRole` | `string` | `"granit-timeline-user"` | Fallback authorization role. |
| `TagName` | `string` | `"Timeline"` | OpenAPI tag. |

---

## Storage

### Blob storage (core) -- `BlobStorageOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `BlobStorage` | |
| **Package** | -- | `Granit.BlobStorage` | |
| `UploadUrlExpiry` | `TimeSpan` | `00:15:00` | Pre-signed upload URL TTL. |
| `DownloadUrlExpiry` | `TimeSpan` | `00:05:00` | Pre-signed download URL TTL. |

### Blob storage S3 -- `S3BlobOptions`

Extends `BlobStorageOptions` with S3-specific settings. Bound from the same
`BlobStorage` section.

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `BlobStorage` | |
| **Package** | -- | `Granit.BlobStorage.S3` | |
| `ServiceUrl` | `string` | `""` | S3-compatible endpoint URL. |
| `AccessKey` | `string` | `""` | S3 access key (from Vault). |
| `SecretKey` | `string` | `""` | S3 secret key (from Vault). |
| `Region` | `string` | `"us-east-1"` | S3 region identifier. |
| `DefaultBucket` | `string` | `""` | Default bucket name. |
| `ForcePathStyle` | `bool` | `true` | Use path-style URLs (required for MinIO). |
| `TenantIsolation` | `BlobTenantIsolation` | `Prefix` | `Prefix` or `BucketPerTenant`. |

### Blob storage Azure -- `AzureBlobOptions`

Extends `BlobStorageOptions` with Azure Blob-specific settings. Bound from the
same `BlobStorage` section.

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `BlobStorage` | |
| **Package** | -- | `Granit.BlobStorage.AzureBlob` | |
| `ConnectionString` | `string` | `""` | Azure Storage connection string (from Vault). |
| `DefaultContainer` | `string` | `""` | Default blob container name. |
| `UseManagedIdentity` | `bool` | `false` | Use Azure Managed Identity instead of connection string. |
| `ServiceUri` | `string` | `""` | Storage account URI (required when `UseManagedIdentity = true`). |
| `TenantIsolation` | `BlobTenantIsolation` | `Prefix` | `Prefix` or `Container` (one per tenant). |

### Blob storage FileSystem -- `FileSystemBlobOptions`

Extends `BlobStorageOptions` with local file system settings. Bound from the
same `BlobStorage` section.

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `BlobStorage` | |
| **Package** | -- | `Granit.BlobStorage.FileSystem` | |
| `BasePath` | `string` | `""` | Root directory for blob storage (required). |

### Blob storage DbStore -- `DbStoreBlobOptions`

Extends `BlobStorageOptions` with database storage settings. Bound from the
same `BlobStorage` section.

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `BlobStorage` | |
| **Package** | -- | `Granit.BlobStorage.DbStore` | |
| `MaxBlobSizeBytes` | `long` | `10485760` (10 MB) | Maximum blob size accepted by the provider. |

### Blob storage Proxy -- `ProxyBlobOptions`

Configuration for the proxy endpoint provider used by FileSystem and DbStore
providers. Bound from the `BlobStorage:Proxy` section.

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `BlobStorage:Proxy` | |
| **Package** | -- | `Granit.BlobStorage.Proxy` | |
| `BaseUrl` | `string` | `""` | Public URL of the API server (required). |
| `RoutePrefix` | `string` | `"/api/blobs"` | Route prefix for proxy endpoints. |
| `MaxUploadBytes` | `long` | `104857600` (100 MB) | Maximum upload size through proxy. |

---

## Scheduling and jobs

### Background jobs -- `BackgroundJobsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `BackgroundJobs` | |
| **Package** | -- | `Granit.BackgroundJobs` | |
| `Mode` | `JobStoreMode` | `InMemory` | `InMemory` (dev) or `Durable` (EF Core). |
| `ConnectionString` | `string` | `""` | DB connection string (required when `Mode` is `Durable`). |

### Background jobs endpoints -- `BackgroundJobsEndpointsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | `BackgroundJobsEndpoints` | |
| **Package** | -- | `Granit.BackgroundJobs.Endpoints` | |
| `RoutePrefix` | `string` | `"background-jobs"` | Route prefix. |
| `RequiredRole` | `string` | `"granit-background-jobs-admin"` | Fallback authorization role. |
| `TagName` | `string` | `"Background Jobs"` | OpenAPI tag. |

---

## Localization

### Localization -- `GranitLocalizationOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | *(configured in code via lambda)* | |
| **Package** | -- | `Granit.Localization` | |
| `EnableAutoDiscovery` | `bool` | `false` | Auto-discover JSON localization resources by naming convention. |

`GranitLocalizationOptions` is primarily configured through code
(`services.AddGranitLocalization(options => ...)`) rather than `appsettings.json`.
Properties like `Languages`, `Resources`, and `FormattingCultures` are populated
programmatically.

### Localization overrides cache -- `LocalizationOverridesCacheOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | *(configured in code)* | |
| **Package** | -- | `Granit.Localization` | |
| `CacheTtl` | `TimeSpan` | `00:05:00` | In-memory TTL for DB override dictionaries. |

### Localization endpoints -- `LocalizationEndpointsOptions`

| Key | Type | Default | Description |
|---|---|---|---|
| **Section** | -- | *(configured in code)* | |
| **Package** | -- | `Granit.Localization.Endpoints` | |
| `RoutePrefix` | `string` | `"localization"` | Route prefix. |
| `TagName` | `string` | `"Localization"` | OpenAPI tag. |
