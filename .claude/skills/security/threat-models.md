# Threat Models — Granit .NET

Pre-built STRIDE threat models for the most critical Granit components.
Used by the `/security` skill as starting points — adapt based on actual
code analysis.

---

## Trust Boundaries

```text
ZONE 1 — Untrusted (Internet)
├── Browser / SPA
├── External AI Agents
└── Webhook callers

ZONE 2 — DMZ (Edge)
├── BFF / YARP Reverse Proxy
├── MCP Server (stdio/SSE endpoint)
├── Rate Limiter
└── CORS / HSTS middleware

ZONE 3 — Internal (Application)
├── API Modules (*.Endpoints)
├── Authorization engine
├── Wolverine message handlers
├── Background jobs
└── Business logic

ZONE 4 — Trusted (Data)
├── PostgreSQL (persistence + outbox)
├── Redis (cache, sessions, rate limiting)
├── Vault (secrets, transit encryption)
└── Blob Storage (S3 / Azure)

ZONE 5 — External Identity
├── Keycloak / EntraId / Cognito / Google
├── OpenIddict (self-hosted IdP)
└── ACME / Certificate authorities
```

---

## TM-01: BFF Authentication Flow

**Data Flow:** Browser → BFF Login → IdP Authorization → BFF Callback → Redis Token Store → YARP → API

### STRIDE Analysis

| Threat | Vector | Mitigation (expected) | Verify |
|--------|--------|----------------------|--------|
| **Spoofing** | Attacker replays stolen session cookie | Session cookie: `Secure`, `HttpOnly`, `SameSite=Strict`; session rotation after login | Check `BffLoginEndpoints` cookie settings |
| **Spoofing** | Attacker forges CSRF token | HMAC-SHA256 with server-side secret, bound to session | Check `HmacBffCsrfTokenGenerator` key source |
| **Tampering** | Attacker modifies `redirect_uri` in auth request | Allowlist-based validation, exact match | Check `BffLoginEndpoints` redirect validation |
| **Tampering** | Attacker modifies token in Redis | Redis connection TLS, token encryption at rest | Check `DistributedCacheBffTokenStore` |
| **Repudiation** | User denies performing an action | Audit trail via `Granit.Auditing`, session correlation | Check audit interceptor coverage |
| **Info Disclosure** | Token leaked via URL, referrer, or logs | Tokens in headers only, `[LoggerMessage]` templates reviewed | Grep for token in query strings or log templates |
| **DoS** | Login endpoint flooded | Rate limiting on `/bff/login` | Check rate limit policy on BFF endpoints |
| **EoP** | Attacker escalates via token injection in YARP header | BFF validates token before injection, YARP strips external auth headers | Check `BffTokenInjectionTransform` |

### Attack Tree: Session Hijacking via BFF

```text
Goal: Steal user session
├── [AND] Obtain session cookie
│   ├── XSS in SPA → steal cookie
│   │   └── Mitigated by: HttpOnly flag
│   ├── Network sniffing
│   │   └── Mitigated by: Secure flag + HSTS
│   └── CSRF to perform actions as user
│       └── Mitigated by: SameSite + CSRF token
├── [OR] Obtain access token from Redis
│   ├── Redis unauthenticated access
│   │   └── Mitigated by: Redis AUTH + TLS
│   └── Redis key enumeration
│       └── Check: Are keys unpredictable?
└── [OR] Forge session
    ├── Predict session ID
    │   └── Check: Session ID entropy >= 128 bits
    └── Session fixation
        └── Check: Session regenerated after login
```

---

## TM-02: MCP Tool Execution Flow

**Data Flow:** AI Agent → MCP Transport → MCP Server → Visibility Filter → Tool Resolution → Authorization → Tool Execution → Output Sanitizer → Response

### STRIDE Analysis

| Threat | Vector | Mitigation (expected) | Verify |
|--------|--------|----------------------|--------|
| **Spoofing** | Attacker impersonates authorized AI agent | MCP transport authentication (API key, mTLS, session) | Check MCP server auth middleware |
| **Spoofing** | Tool executes with wrong user context | `McpTenantScopeAttribute` + permission check on calling user | Check `TenantAwareVisibilityFilter` |
| **Tampering** | Prompt injection in tool parameters | Input validation against schema, parameterized queries | Check tool input handling |
| **Tampering** | Malicious tool response injected | Output sanitizer applied, response signed/integrity-checked | Check `IMcpOutputSanitizer` pipeline |
| **Repudiation** | AI agent denies tool invocation | Audit log of all MCP tool calls | Check MCP audit integration |
| **Info Disclosure** | Tool returns cross-tenant data | Tenant-scoped query filters, `TenantAwareVisibilityFilter` | Check all tool implementations |
| **Info Disclosure** | Error leaks internal details | `ErrorSanitizer` strips stack traces, paths | Check error handling in MCP server |
| **DoS** | Recursive tool calls exhaust resources | Max recursion depth, per-tool timeout, rate limiting | Check MCP tool execution pipeline |
| **EoP** | Tool chaining escalates privileges | Each tool call re-validates permissions independently | Check authorization per-call |
| **Tampering** | SSRF via `IMcpClientFactory` — client connects to internal service | URL allowlist, reject RFC 1918/link-local/localhost IPs | Check `McpConnectionOptions` validation |
| **Info Disclosure** | SSRF response from internal service returned to attacker | Response sanitization, deny internal IP ranges | Check `DefaultMcpClientFactory` IP validation |

### Attack Tree: SSRF via MCP Client

```text
Goal: Access internal services via MCP client connection
├── [OR] Direct SSRF
│   ├── MCP connection URL points to internal IP (10.x, 172.16-31.x, 192.168.x)
│   │   └── Check: IP allowlist/denylist in IMcpClientFactory
│   ├── MCP connection URL points to localhost/127.0.0.1/::1
│   │   └── Check: Loopback rejection
│   └── MCP connection URL points to cloud metadata (169.254.169.254)
│       └── Check: Link-local rejection
├── [OR] DNS rebinding
│   ├── Attacker domain resolves to internal IP after initial validation
│   │   └── Check: Re-validate resolved IP before connecting
│   └── DNS TTL manipulation
│       └── Check: Pin DNS resolution for connection lifetime
└── [OR] Redirect-based SSRF
    ├── MCP server returns HTTP redirect to internal endpoint
    │   └── Check: Client does not follow redirects (or re-validates target)
    └── Credential forwarding to attacker-controlled redirect target
        └── Check: Credentials stripped on redirect to different origin
```

### Attack Tree: Cross-Tenant Data Exfiltration via MCP

```text
Goal: Access Tenant B data from Tenant A context
├── [OR] Bypass tenant visibility filter
│   ├── Tool not decorated with McpTenantScopeAttribute
│   │   └── Check: All data-accessing tools have attribute
│   ├── Filter implementation bug (null tenant = all tenants)
│   │   └── Check: NullTenantContext handling
│   └── Tool uses direct SQL (bypasses EF query filters)
│       └── Check: No raw SQL in MCP tools
├── [OR] Inject tenant ID in parameters
│   ├── Tool accepts tenant ID as parameter
│   │   └── Check: Tenant ID comes from context, not input
│   └── IDOR on entity IDs across tenants
│       └── Check: Entity lookups include tenant filter
└── [OR] Cache poisoning
    ├── Cached tool response from Tenant B served to Tenant A
    │   └── Check: Cache key includes tenant ID
    └── Stale cache after tenant switch
        └── Check: Cache invalidation on tenant context change
```

---

## TM-03: Multi-Tenant Data Access

**Data Flow:** Request → Tenant Resolution → DbContext → Named Query Filters → Entity → Response

### STRIDE Analysis

| Threat | Vector | Mitigation (expected) | Verify |
|--------|--------|----------------------|--------|
| **Spoofing** | Attacker sends `X-Tenant-Id` header for another tenant | JWT claim takes precedence, header only for internal calls | Check resolver pipeline priority |
| **Tampering** | Modify entity's `TenantId` field after creation | `TenantId` has `private set`, set by interceptor | Check entity configuration |
| **Info Disclosure** | Query returns entities from other tenants | Named query filter on `TenantId` via `ApplyGranitConventions` | Check filter registration for all entities |
| **Info Disclosure** | `IgnoreQueryFilters()` used without re-adding tenant clause | Code review: every `IgnoreQueryFilters()` must be justified | Grep all occurrences |
| **Info Disclosure** | `ExecuteUpdate`/`ExecuteDelete` bypasses filters | Must manually add `.Where(e => e.TenantId == tenantId)` | Grep all bulk operations |
| **DoS** | Noisy neighbor: one tenant monopolizes resources | Tenant-partitioned rate limiting, connection pooling | Check resource isolation |
| **EoP** | Tenant admin assigns permissions beyond tenant scope | Permission scope bound to tenant context | Check permission assignment logic |

---

## TM-04: DPoP Token Binding

**Data Flow:** Client generates DPoP proof → Token Request + DPoP header → Server validates proof → Binds token to key → API call + DPoP proof → Server re-validates

### STRIDE Analysis

| Threat | Vector | Mitigation (expected) | Verify |
|--------|--------|----------------------|--------|
| **Spoofing** | Stolen access token used without DPoP proof | Token bound to JWK thumbprint, proof required on every request | Check `DPoPValidationMiddleware` enforcement |
| **Spoofing** | Attacker generates DPoP proof with different key | `cnf.jkt` in token must match proof's JWK thumbprint | Check thumbprint validation |
| **Tampering** | DPoP proof replayed | Nonce tracking (server-issued) or `jti` uniqueness check | Check replay detection mechanism |
| **Tampering** | DPoP proof `htm`/`htu` claims forged for different endpoint | Validate `htm` matches HTTP method, `htu` matches request URI | Check claim validation in middleware |
| **Info Disclosure** | DPoP public key leaks client identity | JWK in proof is ephemeral (not long-lived client cert) | Check key generation guidance |
| **DoS** | DPoP nonce endpoint flooded | Rate limiting on nonce issuance | Check nonce endpoint protection |

---

## TM-05: Crypto-Shredding (GDPR Erasure)

**Data Flow:** Deletion Request → GdprDeletionSaga → ICryptoShredder → Destroy Entity Key → ICryptoShreddingAuditRecorder → Confirm Deletion

### STRIDE Analysis

| Threat | Vector | Mitigation (expected) | Verify |
|--------|--------|----------------------|--------|
| **Spoofing** | Unauthorized deletion request | Request requires authenticated user + self-service or admin permission | Check deletion endpoint authorization |
| **Tampering** | Deletion saga partially executes (key destroyed but audit missed) | Saga uses compensation, atomic transaction for key + audit | Check saga transaction boundaries |
| **Repudiation** | Organization denies deletion was performed | Immutable audit record via `ICryptoShreddingAuditRecorder` | Check audit immutability |
| **Info Disclosure** | Encrypted data recoverable after key destruction | All key copies destroyed (primary, cache, backup references) | Check `ICryptoShredder` completeness |
| **Info Disclosure** | Backups contain unshredded data | Backups themselves encrypted with rotatable keys, or backup retention < GDPR deadline | Check backup strategy |
| **DoS** | Mass deletion request triggers cascading re-encryption | Deletion is shredding (no re-encryption), bounded concurrency | Check saga resource limits |

---

## TM-06: Wolverine Outbox Messaging

**Data Flow:** Domain Event → Outbox (same transaction) → Outbox Agent → Message Broker → Consumer Handler

### STRIDE Analysis

| Threat | Vector | Mitigation (expected) | Verify |
|--------|--------|----------------------|--------|
| **Spoofing** | Fake message injected into outbox table | Outbox table writes via EF Core transaction only (no direct SQL access) | Check outbox table permissions |
| **Tampering** | Message content modified between outbox and consumer | Message integrity (envelope signing or DB-level integrity) | Check message envelope |
| **Repudiation** | Message processed but no trace | W3C Trace Context propagation, audit correlation | Check `TraceContextBehavior` |
| **Info Disclosure** | DLQ messages contain PII exposed to support team | PII redaction in DLQ messages or access controls on DLQ | Check DLQ handling policy |
| **DoS** | Poison message causes infinite retry loop | Max retry count + exponential backoff + dead-letter after N failures | Check retry policy configuration |
| **DoS** | Outbox table grows unbounded (messages not consumed) | Outbox agent polling, metrics on outbox lag, alerting | Check outbox monitoring |
| **EoP** | Consumer handler processes message without restoring user/tenant context | `UserContextBehavior` + `TenantContextBehavior` applied as middleware | Check handler pipeline |

---

## TM-07: API Key Authentication

**Data Flow:** Client → API Key in Header → Middleware → Hash & Lookup → CIDR Validation → Claims Injection → Authorization

### STRIDE Analysis

| Threat | Vector | Mitigation (expected) | Verify |
|--------|--------|----------------------|--------|
| **Spoofing** | Brute-force API key | High entropy (256-bit), rate limiting, account lockout | Check key generation + rate limiting |
| **Spoofing** | Timing attack on key comparison | `CryptographicOperations.FixedTimeEquals` | Check comparison implementation |
| **Spoofing** | X-Forwarded-For spoofing to bypass CIDR | Trust proxy headers only from known load balancer IPs | Check `CidrValidator` IP source |
| **Tampering** | API key cached after revocation | Cache invalidation on revocation event | Check `IApiKeyCacheService` invalidation |
| **Repudiation** | API call made with stolen key | API key tied to service account, all calls audited | Check audit on API key auth |
| **Info Disclosure** | API key logged in plaintext | Key masked in logs (show only last 4 chars) | Check logging templates |
| **DoS** | Key enumeration via timing differences | Constant-time lookup even for non-existent keys | Check lookup implementation |

---

## TM-08: Deserialization Across Trust Boundaries

**Data Flow:** External Input → JSON/Binary Deserializer → Domain Object → Business Logic

Deserialization is a cross-cutting concern affecting multiple components:
Wolverine messages (outbox → consumer), FusionCache (Redis → app),
Claim Check payloads (blob → handler), MCP tool responses (external → server).

### STRIDE Analysis

| Threat | Vector | Mitigation (expected) | Verify |
|--------|--------|----------------------|--------|
| **Tampering** | Attacker injects type discriminator in JSON to instantiate arbitrary types | `System.Text.Json` with closed `[JsonDerivedType]` set, no `TypeNameHandling` | Grep for `TypeNameHandling`, `JsonPolymorphicAttribute` with open type sets |
| **Tampering** | Wolverine outbox message contains crafted payload | Schema-first deserialization with known message types only | Check Wolverine serializer configuration |
| **Tampering** | Cache poisoning with malicious serialized object | FusionCache serializer does not resolve arbitrary types | Check `EncryptingFusionCacheSerializer` chain |
| **Tampering** | Claim Check payload replaced in blob storage | Integrity validation (hash/signature) on retrieval | Check `ClaimCheckReference` validation |
| **DoS** | Deeply nested JSON causes stack overflow | `MaxDepth` configured in `JsonSerializerOptions` | Check global serializer defaults |
| **DoS** | Extremely large payload exhausts memory | Request size limits, message size limits | Check Wolverine + Kestrel limits |
| **EoP** | Deserialized object triggers constructor with side effects | Immutable records, no logic in constructors/init | Check message/DTO design patterns |

### Attack Tree: Deserialization-Based Remote Code Execution

```text
Goal: Execute arbitrary code via crafted serialized payload
├── [OR] Polymorphic JSON deserialization
│   ├── Newtonsoft.Json with TypeNameHandling != None
│   │   └── Check: grep for "Newtonsoft" and "TypeNameHandling"
│   ├── System.Text.Json with open [JsonDerivedType] set
│   │   └── Check: all polymorphic types use closed discriminator list
│   └── Custom IJsonTypeInfoResolver that resolves untrusted types
│       └── Check: custom resolvers restrict type resolution
├── [OR] Wolverine message forgery
│   ├── Direct INSERT into outbox table (SQL injection elsewhere)
│   │   └── Check: outbox table permissions (app user has INSERT only?)
│   ├── Replay modified DLQ message with altered type
│   │   └── Check: DLQ replay validates message schema
│   └── Message type not validated on consumption
│       └── Check: Wolverine handler type mapping is explicit
├── [OR] Cache value poisoning
│   ├── Redis MITM (unencrypted connection)
│   │   └── Check: Redis TLS enabled
│   ├── Direct Redis write (compromised credentials)
│   │   └── Check: Redis ACL with minimal permissions
│   └── Encrypted cache value replaced (breaks integrity)
│       └── Check: authenticated encryption (GCM tag verified)
└── [OR] Claim Check payload swap
    ├── Blob storage object replaced
    │   └── Check: integrity hash stored alongside reference
    └── Claim Check reference points to attacker-controlled blob
        └── Check: blob path validated against expected pattern
```

---

## Methodology Notes

### Using these threat models

1. **Start with the relevant TM** for the domain being audited
2. **Verify each mitigation** exists in the actual code
3. **Follow attack trees** to find gaps
4. **Score findings** with CVSS 3.1
5. **Check compensating controls** before assigning final severity
6. **Update the TM** if new attack vectors are discovered

### Revision triggers — when to revisit a threat model

Threat models are point-in-time artifacts. Revisit when:

| Change | Affected TMs | Why |
|--------|-------------|-----|
| MCP transport changes (stdio → HTTP/SSE) | TM-02 | New network attack surface, authentication model changes |
| New identity provider added | TM-01, TM-04 | New trust boundary, token format differences |
| Redis replaced with another cache | TM-01, TM-03, TM-08 | Encryption-at-rest, authentication model |
| New Wolverine transport (RabbitMQ, Kafka) | TM-06, TM-08 | Message envelope format, authentication, encryption |
| New MCP tool contributor module | TM-02 | New data access patterns, new output sanitization needs |
| Multi-region deployment | TM-03, TM-05 | Cross-region tenant isolation, data residency |
| Public API gateway added | TM-01, TM-07 | New trust boundary, rate limiting changes |
| Serialization library change | TM-08 | Entire deserialization threat model may change |

**Rule:** Any PR that modifies a trust boundary crossing should reference the
relevant TM and confirm mitigations still hold.

### CVSS 3.1 Quick Reference

| Metric | Values |
|--------|--------|
| Attack Vector (AV) | Network (N), Adjacent (A), Local (L), Physical (P) |
| Attack Complexity (AC) | Low (L), High (H) |
| Privileges Required (PR) | None (N), Low (L), High (H) |
| User Interaction (UI) | None (N), Required (R) |
| Scope (S) | Unchanged (U), Changed (C) |
| Confidentiality (C) | None (N), Low (L), High (H) |
| Integrity (I) | None (N), Low (L), High (H) |
| Availability (A) | None (N), Low (L), High (H) |

Example: `CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:C/C:H/I:H/A:N` = 10.0 (Critical)
