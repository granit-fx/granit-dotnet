# Security Audit Checklist — Granit .NET

Detailed verification matrix used by the `/security` skill. Each section maps
to a `<domain>` keyword. Apply methodically — check each item, note evidence.

Standards referenced:

- **ASVS** — OWASP Application Security Verification Standard 4.0
- **FAPI** — Financial-grade API Security Profile 2.0
- **ISO** — ISO 27001:2022 Annex A
- **LLM** — OWASP LLM Top 10 (2025)
- **RFC** — IETF RFCs (9449, 9126, 7519, 6749, 7636, etc.)
- **CWE** — Common Weakness Enumeration

---

## 1. Identity & Access Management (`iam`)

### 1a. BFF Pattern — Granit.Bff

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 1.1 | CSRF token uses HMAC-SHA256 with cryptographically random key | ASVS 4.2.2 | HIGH |
| 1.2 | CSRF key is rotated periodically (not hardcoded) | ISO A.8.24 | CRITICAL |
| 1.3 | CSRF token is bound to user session (not global) | ASVS 4.2.2 | HIGH |
| 1.4 | BFF token store encrypts tokens at rest in Redis | ASVS 6.4.2 | CRITICAL |
| 1.5 | Session ID regenerated after authentication | ASVS 3.2.1 | HIGH |
| 1.6 | Silent token refresh uses `lock` to prevent concurrent refreshes | CWE-362 | MEDIUM |
| 1.7 | Refresh token rotation invalidates previous token | ASVS 3.5.2 | HIGH |
| 1.8 | Back-channel logout validates `logout_token` JWT signature | RFC 7519 | CRITICAL |
| 1.9 | Back-channel logout checks `iss`, `aud`, `iat`, `events` claims | ASVS 3.6.1 | HIGH |
| 1.10 | `Set-Cookie` flags: `Secure`, `HttpOnly`, `SameSite=Strict/Lax` | ASVS 3.4.1 | HIGH |
| 1.11 | Token injection in YARP uses secure header (not query string) | ASVS 3.1.1 | MEDIUM |
| 1.12 | BFF endpoints reject requests without valid session | ASVS 3.3.1 | HIGH |
| 1.13 | Login endpoint validates `redirect_uri` against allowlist | CWE-601 | CRITICAL |
| 1.14 | Logout revokes tokens at the IdP (not just local session) | ASVS 3.3.3 | HIGH |

### 1b. DPoP — Granit.Authentication.DPoP

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 1.15 | DPoP proof JWT validated: `typ`, `alg`, `jwk`, `htm`, `htu`, `iat` | RFC 9449 §4.3 | CRITICAL |
| 1.16 | `ath` claim validated (access token hash) | RFC 9449 §4.3 | HIGH |
| 1.17 | JWK thumbprint (S256) matches token `cnf.jkt` claim | RFC 9449 §6 | CRITICAL |
| 1.18 | Nonce is server-generated, single-use, time-bound | RFC 9449 §8 | HIGH |
| 1.19 | DPoP proof replay detection (nonce or `jti` tracking) | RFC 9449 §11.1 | HIGH |
| 1.20 | Clock skew tolerance is bounded (max 60s recommended) | RFC 9449 §4.3 | MEDIUM |
| 1.21 | Only asymmetric algorithms accepted (no `HS256`) | RFC 9449 §4.3 | CRITICAL |
| 1.22 | Middleware cannot be bypassed via route configuration | CWE-284 | HIGH |

### 1c. OIDC / OpenIddict

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 1.23 | PKCE mandatory with `S256` method (never `plain`) | RFC 7636, FAPI §5.2.2 | CRITICAL |
| 1.24 | Authorization code is single-use | RFC 6749 §4.1.2 | HIGH |
| 1.25 | Token endpoint requires client authentication | FAPI §5.2.2 | HIGH |
| 1.26 | `private_key_jwt` or mTLS preferred over `client_secret_post` | FAPI §5.2.2 | MEDIUM |
| 1.27 | ID token `nonce` validated against session-stored value | ASVS 3.5.1 | HIGH |
| 1.28 | ID token `at_hash` validated for implicit/hybrid flows | RFC 9207 | MEDIUM |
| 1.29 | Token lifetime is bounded (access: 5-15min, refresh: 24h max) | ASVS 3.5.3 | MEDIUM |
| 1.30 | Signing keys use RS256 or ES256 (never HS256 for public clients) | FAPI §5.2.2 | CRITICAL |
| 1.31 | Key rotation job overlaps old and new keys (grace period) | ISO A.8.24 | HIGH |
| 1.32 | PAR (Pushed Authorization Requests) supported | RFC 9126, FAPI §5.2.2 | MEDIUM |
| 1.33 | `redirect_uri` exact match (no wildcard, no path traversal) | CWE-601 | CRITICAL |
| 1.34 | Impersonation requires elevated privilege AND audit logging | ASVS 4.1.3 | CRITICAL |
| 1.35 | Impersonation scope is limited (no admin-over-admin) | CWE-269 | HIGH |
| 1.36 | Token cleanup job removes expired tokens within SLA | ISO A.8.10 | LOW |

### 1d. API Key Authentication

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 1.37 | API key generated with >= 256 bits of entropy | ASVS 2.4.1 | CRITICAL |
| 1.38 | API key stored hashed (SHA-256 or better), never plaintext | ASVS 2.4.1 | CRITICAL |
| 1.39 | Key comparison is timing-safe (`CryptographicOperations.FixedTimeEquals`) | CWE-208 | HIGH |
| 1.40 | CIDR validation cannot be bypassed via `X-Forwarded-For` spoofing | CWE-290 | HIGH |
| 1.41 | Revoked keys are immediately removed from cache | ASVS 2.10.2 | HIGH |
| 1.42 | Key type (service/user/readonly) enforced at authorization layer | ASVS 4.1.1 | MEDIUM |
| 1.43 | API key transmission only via header (never URL query parameter) | ASVS 3.1.1 | MEDIUM |

### 1e. Authorization

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 1.44 | Default-deny: endpoints require explicit authorization | ASVS 4.1.1 | CRITICAL |
| 1.45 | `[AllowAnonymous]` endpoints are inventoried and justified | ASVS 4.1.1 | HIGH |
| 1.46 | Permission cache TTL is short enough to reflect revocation | ASVS 4.1.3 | MEDIUM |
| 1.47 | Permission assignment requires the assigner to hold the permission | CWE-269 | HIGH |
| 1.48 | `DynamicPermissionPolicyProvider` rejects unknown permission names | CWE-284 | MEDIUM |
| 1.49 | Permission changes emit integration events for downstream sync | ASVS 4.1.3 | LOW |
| 1.50 | Horizontal privilege escalation: user cannot access other users' resources | CWE-639 | CRITICAL |

### 1f. Identity & Password Management

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 1.51 | Password hashing uses Argon2id, bcrypt, or PBKDF2 (>= 600K iterations) | ASVS 2.4.1 | CRITICAL |
| 1.52 | Password complexity enforced (min 8 chars, no max limit < 128) | ASVS 2.1.1 | MEDIUM |
| 1.53 | Account lockout after N failed attempts (with exponential backoff) | ASVS 2.2.1 | HIGH |
| 1.54 | Password reset tokens are single-use, time-bound (< 1h) | ASVS 2.5.2 | HIGH |
| 1.55 | User enumeration prevented (identical responses for existing/non-existing) | CWE-204 | MEDIUM |
| 1.56 | Session revocation on password change | ASVS 3.3.4 | HIGH |
| 1.57 | MFA/TOTP implementation uses time-based tokens (RFC 6238) | ASVS 2.8.1 | MEDIUM |
| 1.58 | Passkey/WebAuthn support validates attestation | ASVS 2.7.1 | LOW |

---

## 2. AI & MCP Security (`ai`)

### 2a. MCP Tool Discovery & Visibility

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 2.1 | Default discovery mode is explicit (opt-in, not opt-out) | LLM07 | CRITICAL |
| 2.2 | `McpExposedAttribute` required for tool exposure | LLM07 | HIGH |
| 2.3 | `TenantAwareVisibilityFilter` enforces tenant isolation | LLM06 | CRITICAL |
| 2.4 | `ModuleScopeVisibilityFilter` restricts tools to loaded modules | LLM08 | HIGH |
| 2.5 | Tool descriptions do not leak internal architecture details | LLM06 | MEDIUM |
| 2.6 | Tool parameter schemas validate input types and ranges | LLM01 | HIGH |
| 2.7 | MCP transport (stdio/SSE/HTTP) uses authenticated channel | LLM07 | HIGH |

### 2b. Output Sanitization

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 2.8 | `IMcpOutputSanitizer` applied to ALL tool responses (not opt-in) | LLM02 | CRITICAL |
| 2.9 | `McpRedactAttribute` coverage: all PII/secret fields annotated | LLM06 | HIGH |
| 2.10 | `RedactionStrategy` includes full redaction (not just masking) for secrets | LLM06 | HIGH |
| 2.11 | Error responses sanitized (no stack traces, connection strings, paths) | LLM06 | HIGH |
| 2.12 | SQL query results sanitized for cross-tenant data | LLM06 | CRITICAL |
| 2.13 | File system paths in responses are relative (no absolute paths) | LLM06 | MEDIUM |

### 2c. Prompt Injection & Confused Deputy

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 2.14 | MCP tool inputs validated against schema before execution | LLM01 | CRITICAL |
| 2.15 | User-controlled strings not interpolated into tool descriptions | LLM01 | CRITICAL |
| 2.16 | Tool execution checks CALLING user's permissions (not tool owner) | LLM08 | CRITICAL |
| 2.17 | `McpTenantScopeAttribute` enforced — tools respect tenant context | LLM08 | CRITICAL |
| 2.18 | MCP tool calls are audited (who, what, when, result summary) | LLM07 | HIGH |
| 2.19 | Tool chaining cannot escalate privileges across calls | LLM08 | HIGH |
| 2.20 | Content returned by tools is treated as untrusted by the LLM layer | LLM01 | HIGH |

### 2d. Resource Exhaustion & Denial of Wallet

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 2.21 | MCP tool calls are rate-limited per user/tenant | LLM08 | HIGH |
| 2.22 | Expensive operations (DB queries, file I/O) have timeouts | LLM08 | HIGH |
| 2.23 | Token/cost budget per session or per user | LLM08 | MEDIUM |
| 2.24 | Recursive tool calls are bounded (max depth) | LLM08 | HIGH |
| 2.25 | Large result sets are paginated or truncated | LLM06 | MEDIUM |

### 2e. MCP Client-Side Risks (SSRF)

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 2.26 | `McpConnectionOptions` URL validated against allowlist (no internal IPs) | CWE-918 | CRITICAL |
| 2.27 | `IMcpClientFactory` rejects connections to `localhost`, `127.0.0.1`, `::1`, `169.254.*` (link-local), RFC 1918 ranges | CWE-918 | CRITICAL |
| 2.28 | MCP client does not follow redirects to internal endpoints | CWE-918 | HIGH |
| 2.29 | DNS rebinding protection: re-validate resolved IP before connecting | CWE-918 | HIGH |
| 2.30 | MCP client connection timeout bounded (prevent slowloris against internal) | CWE-400 | MEDIUM |
| 2.31 | MCP client credentials (API key, token) not sent to untrusted servers | CWE-522 | CRITICAL |

---

## 3. Data Protection & Encryption (`data`)

### 3a. Encryption at Rest

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 3.1 | AES encryption uses GCM mode (authenticated encryption) | ASVS 6.2.1 | CRITICAL |
| 3.2 | IV/nonce is unique per encryption operation (never reused) | CWE-329 | CRITICAL |
| 3.3 | Key derivation uses a KDF (HKDF, PBKDF2) — not raw key | ASVS 6.2.2 | HIGH |
| 3.4 | Encryption keys >= 256 bits | NIST SP 800-57 | HIGH |
| 3.5 | `InMemoryEntityEncryptionKeyStore` guarded: dev/test only | ASVS 6.4.1 | CRITICAL |
| 3.6 | Cache values encrypted with `AesCacheValueEncryptor` use unique IVs | CWE-329 | HIGH |
| 3.7 | Encrypted fields in database use a separate column for IV/tag | ASVS 6.2.1 | HIGH |

### 3b. Key Management

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 3.8 | Encryption keys stored in Vault (never in config/env vars) | ISO A.8.24 | CRITICAL |
| 3.9 | Key rotation supported without data re-encryption downtime | ISO A.8.24 | HIGH |
| 3.10 | `RetiredKeyVersionException` handled gracefully (re-encrypt, not fail) | ISO A.8.24 | HIGH |
| 3.11 | Envelope encryption: data key encrypted by master key | ASVS 6.4.1 | MEDIUM |
| 3.12 | Key destruction zeroizes memory (`CryptographicOperations.ZeroMemory`) | CWE-244 | HIGH |
| 3.13 | `IDatabaseCredentialProvider` rotates credentials before TTL expiry | ISO A.8.24 | HIGH |
| 3.14 | Transit encryption (`ITransitEncryptionService`) uses Vault's transit backend | ISO A.8.24 | MEDIUM |

### 3c. Crypto-Shredding (GDPR Art. 17)

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 3.15 | `ICryptoShredder` destroys ALL copies of the entity encryption key | GDPR Art. 17 | CRITICAL |
| 3.16 | Crypto-shredding audit trail (`ICryptoShreddingAuditRecorder`) is immutable | ISO A.8.15 | HIGH |
| 3.17 | Shredding covers backup keys (or backups are themselves encrypted) | GDPR Art. 17 | HIGH |
| 3.18 | Shredding is atomic (key destruction + audit entry in one transaction) | ASVS 8.3.7 | HIGH |

### 3d. Privacy / GDPR

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 3.19 | `GdprDeletionSaga` has compensation on failure (no partial deletion) | GDPR Art. 17 | CRITICAL |
| 3.20 | `GdprExportSaga` encrypts exported data before transmission | GDPR Art. 20 | HIGH |
| 3.21 | `IDataProviderRegistry` covers ALL modules with personal data | GDPR Art. 17 | HIGH |
| 3.22 | Data export format is machine-readable (JSON/CSV) | GDPR Art. 20 | LOW |
| 3.23 | Deletion request processing completes within 30 days (SLA) | GDPR Art. 12 | MEDIUM |
| 3.24 | Consent records (`LegalAgreements`) are tamper-proof | GDPR Art. 7 | HIGH |
| 3.25 | Consent revocation triggers downstream data processing stop | GDPR Art. 7.3 | HIGH |

---

## 4. Multi-Tenancy Isolation (`tenancy`)

### 4a. Tenant Resolution

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 4.1 | `HeaderTenantResolver` only trusted on internal network (not public) | CWE-290 | CRITICAL |
| 4.2 | `JwtClaimTenantResolver` validates tenant against known tenant list | CWE-284 | HIGH |
| 4.3 | Default behavior when no resolver matches: REJECT (not default tenant) | CWE-284 | CRITICAL |
| 4.4 | Tenant context is immutable once set for the request | CWE-284 | HIGH |
| 4.5 | Tenant resolution logged for audit trail | ISO A.5.15 | MEDIUM |

### 4b. Data Isolation

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 4.6 | Named query filters (`ApplyGranitConventions`) applied to ALL entities | CWE-639 | CRITICAL |
| 4.7 | `IgnoreQueryFilters()` usage audited — grep and justify each occurrence | CWE-639 | HIGH |
| 4.8 | `ExecuteUpdate`/`ExecuteDelete` manually adds tenant WHERE clause | CWE-639 | CRITICAL |
| 4.9 | Database migrations do not alter tenant filter indexes | CWE-639 | MEDIUM |
| 4.10 | Bulk operations verify tenant context before execution | CWE-639 | HIGH |

### 4c. Cross-Service Isolation

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 4.11 | Cache keys include tenant ID prefix (`{tenantId}:{module}:{key}`) | CWE-639 | CRITICAL |
| 4.12 | Wolverine messages propagate tenant context (`TenantContextBehavior`) | CWE-639 | CRITICAL |
| 4.13 | Blob storage paths include tenant partition | CWE-639 | HIGH |
| 4.14 | Rate limiting is tenant-partitioned (`TenantPartitionedRateLimiter`) | CWE-639 | HIGH |
| 4.15 | MCP tool responses scoped to calling tenant | CWE-639 | CRITICAL |
| 4.16 | Background jobs restore tenant context before execution | CWE-639 | HIGH |
| 4.17 | Integration events include `TenantId` for cross-module routing | CWE-639 | HIGH |

---

## 5. Infrastructure & Resilience (`infra`)

### 5a. Wolverine Messaging

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 5.1 | Outbox messages encrypted at rest if containing PII | GDPR Art. 32 | HIGH |
| 5.2 | Dead-letter queue messages reviewed for PII before manual replay | GDPR Art. 32 | MEDIUM |
| 5.3 | Poison message detection: max retry count with exponential backoff | CWE-400 | HIGH |
| 5.4 | Message handlers are idempotent (at-least-once delivery) | CWE-400 | HIGH |
| 5.5 | W3C Trace Context propagated (`TraceContextBehavior`) | ISO A.8.15 | MEDIUM |
| 5.6 | User context restored in async handlers (`UserContextBehavior`) | CWE-284 | HIGH |
| 5.7 | Claim Check pattern validates payload integrity on retrieval | CWE-345 | MEDIUM |
| 5.8 | Large messages stored via `IClaimCheckStore`, not inline | CWE-400 | MEDIUM |

### 5b. Rate Limiting

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 5.9 | Rate limiting applied BEFORE authentication (to protect auth endpoint) | CWE-307 | HIGH |
| 5.10 | `CounterStoreFailureBehavior` is CLOSED (deny on Redis failure) | CWE-636 | HIGH |
| 5.11 | Redis Lua scripts are atomic (no TOCTOU race conditions) | CWE-362 | HIGH |
| 5.12 | Rate limit headers returned (`Retry-After`, `X-RateLimit-*`) | ASVS 13.1.5 | LOW |
| 5.13 | Per-IP rate limiting for unauthenticated endpoints | CWE-307 | HIGH |
| 5.14 | Rate limit policies are not bypassable via header manipulation | CWE-290 | MEDIUM |

### 5c. Idempotency

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 5.15 | Idempotency key has sufficient entropy (UUID v4 or better) | CWE-330 | MEDIUM |
| 5.16 | Idempotency window is bounded (TTL prevents indefinite storage) | CWE-400 | MEDIUM |
| 5.17 | Stored responses do not leak data to different users with same key | CWE-639 | HIGH |
| 5.18 | Idempotency store is tenant-partitioned | CWE-639 | HIGH |

### 5d. Caching Security

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 5.19 | Cache keys are not constructable from user input (injection) | CWE-74 | HIGH |
| 5.20 | Serialized cache values use safe deserializer (no type resolution) | CWE-502 | CRITICAL |
| 5.21 | FusionCache stampede protection enabled | CWE-400 | MEDIUM |
| 5.22 | Stale-while-revalidate does not serve expired security decisions | CWE-613 | HIGH |
| 5.23 | Encrypted cache (`CacheEncryptedAttribute`) uses authenticated encryption | ASVS 6.2.1 | HIGH |

### 5e. Webhooks

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 5.24 | Outbound webhooks sign payload (HMAC-SHA256 with shared secret) | CWE-345 | HIGH |
| 5.25 | Inbound webhooks validate signature before processing | CWE-345 | CRITICAL |
| 5.26 | Webhook URLs validated (no SSRF: reject internal IPs, localhost) | CWE-918 | CRITICAL |
| 5.27 | Webhook retry does not replay sensitive data indefinitely | GDPR Art. 32 | MEDIUM |

---

## 6. Supply Chain (`supply-chain`)

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 6.1 | All NuGet packages have lock files (`packages.lock.json`) | CWE-1357 | HIGH |
| 6.2 | No known CVEs in dependency tree (`dotnet list package --vulnerable`) | CWE-1357 | CRITICAL |
| 6.3 | No deprecated packages (`dotnet list package --deprecated`) | CWE-1357 | MEDIUM |
| 6.4 | Non-permissive licenses flagged (GPL, LGPL, AGPL, SSPL) | Legal | HIGH |
| 6.5 | `THIRD-PARTY-NOTICES.md` matches actual dependency tree | Legal | MEDIUM |
| 6.6 | NuGet packages are from trusted sources (nuget.org, verified publishers) | CWE-1357 | HIGH |
| 6.7 | No `<PackageReference>` to prerelease packages in Release config | CWE-1357 | MEDIUM |
| 6.8 | Source generators are pinned to exact versions | CWE-1357 | HIGH |
| 6.9 | CI pipeline uses OIDC federation (no long-lived secrets) | ISO A.5.17 | MEDIUM |
| 6.10 | Build is reproducible (deterministic compilation) | CWE-1357 | LOW |
| 6.11 | Pre-commit secret scanning enabled (`gitleaks` or `trufflehog`) | CWE-798 | HIGH |
| 6.12 | CI pipeline runs secret scanning on every push | CWE-798 | HIGH |
| 6.13 | Historical secrets in git history identified and rotated | CWE-798 | MEDIUM |
| 6.14 | `.gitleaksignore` or baseline file reviewed (no blanket suppressions) | CWE-798 | MEDIUM |

---

## 7. Cryptographic Correctness (`crypto`)

### 7a. Algorithm Inventory

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 7.1 | No MD5 or SHA-1 for security purposes (only checksums) | ASVS 6.2.5 | CRITICAL |
| 7.2 | AES uses GCM or CCM mode (never ECB, never CBC without HMAC) | ASVS 6.2.1 | CRITICAL |
| 7.3 | RSA key size >= 2048 bits (4096 preferred) | NIST SP 800-57 | HIGH |
| 7.4 | ECDSA uses P-256 or P-384 curves (not P-192) | NIST SP 800-57 | HIGH |
| 7.5 | HMAC uses SHA-256 or SHA-512 (not SHA-1) | ASVS 6.2.5 | HIGH |

### 7b. Random Number Generation

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 7.6 | `RandomNumberGenerator` used (never `System.Random` for security) | CWE-330 | CRITICAL |
| 7.7 | Token generation uses >= 128 bits of entropy | ASVS 2.4.1 | HIGH |
| 7.8 | Nonce generation is non-repeating (counter or random) | CWE-330 | HIGH |

### 7c. Key Lifecycle

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 7.9 | Keys never logged, never in error messages | CWE-532 | CRITICAL |
| 7.10 | Key material zeroized after use (Span, ArrayPool return) | CWE-244 | HIGH |
| 7.11 | Key rotation is automated (background job or Vault) | ISO A.8.24 | HIGH |
| 7.12 | Retired keys support decryption (re-encrypt on read) | ISO A.8.24 | MEDIUM |
| 7.13 | Key hierarchy: master key > data encryption key > field key | ISO A.8.24 | MEDIUM |

---

## 8. Observability Security (`observability`)

### 8a. Logging

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 8.1 | No PII in log messages (names, emails, phone numbers) | GDPR Art. 5 | CRITICAL |
| 8.2 | No secrets in log messages (tokens, keys, passwords) | CWE-532 | CRITICAL |
| 8.3 | `AuditSensitiveAttribute` applied to all sensitive entity properties | GDPR Art. 5 | HIGH |
| 8.4 | Structured logging does not include raw request bodies | GDPR Art. 5 | HIGH |
| 8.5 | Exception details redacted in production (no stack traces to client) | CWE-209 | HIGH |
| 8.6 | Log injection prevented (user input not directly in log templates) | CWE-117 | MEDIUM |

### 8b. Metrics

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 8.7 | No user-controlled values as metric tag VALUES (cardinality explosion) | CWE-400 | HIGH |
| 8.8 | Metric names do not reveal internal architecture | CWE-200 | LOW |
| 8.9 | Health check endpoints do not expose sensitive system info | CWE-200 | MEDIUM |
| 8.10 | Health checks have timeout (10s) to prevent hung probes | CWE-400 | MEDIUM |

### 8c. Distributed Tracing

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 8.11 | Trace context does not propagate to untrusted external systems | CWE-200 | MEDIUM |
| 8.12 | Span attributes do not contain PII or secrets | GDPR Art. 5 | HIGH |
| 8.13 | Audit entries include trace ID for correlation | ISO A.8.15 | LOW |

### 8d. Audit Trail

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 8.14 | Audit log is append-only (no update/delete) | ISO A.8.15 | CRITICAL |
| 8.15 | Audit log captures: who, what, when, where, result | ISO A.8.15 | HIGH |
| 8.16 | Security events (login, logout, permission change) are audited | ASVS 7.1.1 | HIGH |
| 8.17 | Failed authentication attempts are logged with rate limiting context | ASVS 7.2.1 | HIGH |
| 8.18 | Audit log retention meets regulatory requirements (>= 1 year) | ISO A.8.15 | MEDIUM |
| 8.19 | `AuditEntityChange` captures before/after values for accountability | ISO A.8.15 | MEDIUM |

---

## 9. HTTP Security Headers & Transport (`headers`)

### 9a. Response Headers

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 9.1 | `Strict-Transport-Security` with `max-age >= 31536000; includeSubDomains` | ASVS 9.1.1 | HIGH |
| 9.2 | `Content-Security-Policy` restricts `script-src`, `style-src`, `frame-ancestors` | ASVS 14.4.3 | HIGH |
| 9.3 | `X-Content-Type-Options: nosniff` on all responses | ASVS 14.4.4 | MEDIUM |
| 9.4 | `X-Frame-Options: DENY` (or `SAMEORIGIN` if iframes needed) | ASVS 14.4.7 | MEDIUM |
| 9.5 | `Referrer-Policy: strict-origin-when-cross-origin` (or `no-referrer`) | ASVS 14.4.5 | MEDIUM |
| 9.6 | `Permissions-Policy` restricts camera, microphone, geolocation, etc. | ASVS 14.4.6 | LOW |
| 9.7 | `Cache-Control: no-store` on authenticated API responses | ASVS 14.4.2 | HIGH |
| 9.8 | No `Server` or `X-Powered-By` headers leaking technology stack | CWE-200 | LOW |
| 9.9 | YARP proxy strips sensitive upstream headers before returning to client | CWE-200 | MEDIUM |

### 9b. CORS

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 9.10 | No wildcard `*` origin for authenticated endpoints | CWE-942 | HIGH |
| 9.11 | `Access-Control-Allow-Credentials` only with explicit origin list | CWE-942 | HIGH |
| 9.12 | Preflight `Access-Control-Max-Age` bounded (< 7200s) | CWE-942 | LOW |

---

## 10. Deserialization Safety (`deserialization`)

### 10a. JSON Deserialization

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 10.1 | `System.Text.Json` used (not Newtonsoft with `TypeNameHandling`) | CWE-502 | CRITICAL |
| 10.2 | No `JsonSerializerOptions` with `TypeInfoResolver` allowing arbitrary types | CWE-502 | CRITICAL |
| 10.3 | Polymorphic deserialization uses `[JsonDerivedType]` with closed type set | CWE-502 | HIGH |
| 10.4 | `JsonSerializerOptions.MaxDepth` set (default 64 is acceptable) | CWE-400 | MEDIUM |

### 10b. Wolverine Message Deserialization

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 10.5 | Message envelope uses schema-first deserialization (known types only) | CWE-502 | CRITICAL |
| 10.6 | Outbox messages cannot contain polymorphic payloads from external input | CWE-502 | HIGH |
| 10.7 | Dead-letter queue replay validates message schema before re-processing | CWE-502 | HIGH |
| 10.8 | `ClaimCheckReference` payload deserialized with type validation | CWE-502 | HIGH |

### 10c. Cache Deserialization

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 10.9 | FusionCache serializer does not resolve arbitrary types | CWE-502 | CRITICAL |
| 10.10 | `EncryptingFusionCacheSerializer` validates integrity before deserializing | CWE-502 | HIGH |
| 10.11 | Redis cache values cannot trigger type instantiation on deserialization | CWE-502 | HIGH |

---

## 11. Cross-Cutting Concerns

These checks apply across all domains:

| # | Check | Standard | Severity if missing |
|---|-------|----------|---------------------|
| 11.1 | No hardcoded secrets (connection strings, API keys, passwords) in code | CWE-798 | CRITICAL |
| 11.2 | `appsettings.json` / `appsettings.Development.json` contain no secrets | CWE-798 | CRITICAL |
| 11.3 | `.gitignore` excludes sensitive files (`.env`, `*.pfx`, `*.key`) | CWE-798 | HIGH |
| 11.4 | All external HTTP calls use HTTPS (no plain HTTP) | ASVS 9.1.1 | HIGH |
| 11.5 | Content-Type validation on all input endpoints | CWE-20 | MEDIUM |
| 11.6 | Request size limits configured (to prevent large payload DoS) | CWE-400 | MEDIUM |
| 11.7 | Dependency injection: no service locator anti-pattern for security services | CWE-284 | LOW |
| 11.8 | Error responses use RFC 7807 Problem Details (no internal data leak) | CWE-209 | MEDIUM |
| 11.9 | All `async` methods accept and forward `CancellationToken` | CWE-400 | LOW |
| 11.10 | Architecture tests in `Granit.ArchitectureTests` cover security conventions | ISO A.8.25 | MEDIUM |
