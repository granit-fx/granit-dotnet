---
title: "GDPR by Design: Privacy Patterns in a .NET Framework"
date: 2026-02-25
sidebar:
  order: 5
authors:
  - jfmeyers
tags:
  - security
  - compliance
description: "GDPR compliance is not a checkbox. It is an architectural constraint enforced at the framework level. Here is how Granit implements data minimization, right to erasure, and pseudonymization by default."
excerpt: >
  GDPR compliance is not a checkbox. It is an architectural constraint that must
  be enforced at the framework level. Here is how Granit implements data minimization,
  right to erasure, and pseudonymization by default.
---

A developer adds a `DELETE FROM Patients WHERE Id = @id` query in a service class. The feature works. The code review passes. Six months later, during an ISO 27001 audit, someone asks: "Can you prove this patient's data was deleted? Who deleted it? When? Was the deletion propagated to blob storage and notification logs?" The answer is no, because a physical delete leaves no trace.

This is the core problem with treating privacy as an application-level concern. Individual developers make individual decisions about deletion, encryption, and data retention. Some get it right. Most do not, because the framework does not force them to.

Granit takes a different approach. **Privacy is a framework constraint, not an application feature.** The patterns described here are not optional plugins. They are interceptors, query filters, and abstractions that activate by default when you implement the right interfaces.

## Data minimization: collect only what you need, expose only what you must

GDPR Article 5(1)(c) requires **data minimization** -- personal data must be adequate, relevant, and limited to what is necessary. At the framework level, Granit enforces this in two places.

**Identity cache, not identity copy.** `Granit.Identity.EntityFrameworkCore` maintains a user cache that syncs at login time using the cache-aside pattern. The application never stores a full copy of the identity provider's user record. When a user logs in, the cache updates with only the fields the application needs. When a user is deleted from the identity provider, the next sync attempt finds nothing and the cache entry can be cleaned up.

```csharp title="LoginSyncHandler.cs"
public class LoginSyncHandler(
    IIdentityProvider identityProvider,
    IUserCacheWriter userCacheWriter)
{
    public async Task HandleAsync(
        UserLoggedInEvent @event, CancellationToken cancellationToken)
    {
        var user = await identityProvider
            .GetByIdAsync(@event.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null) return;

        // Only DisplayName and Email are cached -- no address, no phone, no SSN.
        await userCacheWriter.UpsertAsync(
            @event.UserId, user.DisplayName, user.Email, cancellationToken)
            .ConfigureAwait(false);
    }
}
```

The identity provider (Keycloak) remains the single source of truth. The application cache is a projection, not a replica.

**Endpoint DTOs, never entities.** Granit enforces a strict rule: EF Core entities must never be returned directly from API endpoints. Every response goes through a `*Response` record. This is not just a code style preference -- it is a data minimization mechanism. The response record declares exactly which fields leave the system boundary. Internal fields like `TenantId`, `DeletedBy`, or `IsProcessingRestricted` never appear in the API surface unless explicitly mapped.

## Right to erasure: soft delete as the default, hard delete as the exception

GDPR Article 17 grants data subjects the right to erasure. The naive implementation is `DELETE FROM`. The correct implementation depends on your regulatory context. In healthcare and finance, you often need to retain records for legal periods while making them invisible to normal operations. Granit handles this with a two-tier strategy.

### Tier 1: Soft delete for operational invisibility

Any entity implementing `ISoftDeletable` is never physically deleted during normal operations. The `SoftDeleteInterceptor` intercepts EF Core's `SaveChanges` call and converts every `DELETE` into an `UPDATE`:

```
DELETE FROM Patients WHERE Id = @id
  -- intercepted --
UPDATE Patients SET IsDeleted = true, DeletedAt = @now, DeletedBy = @userId WHERE Id = @id
```

A global query filter then excludes soft-deleted records from all queries:

```csharp title="ApplyGranitConventions — automatic filter"
// Applied by ApplyGranitConventions for every ISoftDeletable entity
// Expression: e => !e.IsDeleted
```

The developer does not write `WHERE IsDeleted = false`. The developer does not remember to check deletion status. **The framework handles it.** Every query, every navigation property, every `Include` -- all filtered automatically.

When you need to see deleted records (admin panel, audit trail, GDPR export), you disable the filter explicitly:

```csharp title="PatientAdminService.cs"
public class PatientAdminService(IDataFilter dataFilter, AppDbContext db)
{
    public async Task<List<Patient>> GetAllIncludingDeletedAsync(
        CancellationToken cancellationToken)
    {
        using (dataFilter.Disable<ISoftDeletable>())
        {
            return await db.Patients
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
```

The `using` block makes the intent explicit and auditable. The filter re-enables automatically when the scope exits.

### Tier 2: Hard delete for actual erasure

Soft delete is not erasure. GDPR Article 17 requires actual deletion when there is no legal basis for retention. Granit.Privacy provides a **deletion saga** that orchestrates hard deletion across all registered data providers:

```
User -> API: DELETE /privacy/my-data
API -> Saga: PersonalDataDeletionRequestedEto
Saga -> Identity: DeleteByIdAsync (hard delete)
Saga -> BlobStorage: Delete user blobs
Saga -> Notifications: Delete delivery records
Saga -> User: Acknowledgment notification
```

Each module registers itself as a data provider during startup. When a deletion request arrives, the saga queries all registered providers and waits for each to confirm completion. No module is forgotten, because registration is mandatory for modules that hold personal data.

This two-tier approach satisfies both requirements: operational invisibility during normal use (soft delete), and provable erasure when a data subject exercises their rights (hard delete via saga).

## Processing restriction: GDPR Article 18

Between "active" and "deleted" there is a third state that many frameworks ignore: **restricted processing**. GDPR Article 18 allows data subjects to request that their data be kept but not processed -- for example, while a complaint is being investigated.

Granit models this with the `IProcessingRestrictable` interface. Entities implementing it gain an `IsProcessingRestricted` boolean, and `ApplyGranitConventions` adds a query filter that excludes them from normal queries:

```csharp title="Entity with processing restriction support"
public class Patient : AuditedEntity, ISoftDeletable, IProcessingRestrictable, IMultiTenant
{
    public string Name { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public bool IsProcessingRestricted { get; set; }
    public Guid? TenantId { get; set; }
}
```

The query filter (`!e.IsProcessingRestricted`) ensures that restricted records are invisible to business logic without being deleted. An admin can lift the restriction later, and the data reappears. No restore operation, no backup retrieval -- just a flag change.

This is a subtle but important distinction from soft delete. Soft-deleted data is gone from the application's perspective. Processing-restricted data is preserved but frozen. Both states are enforced at the query filter level, which means application code cannot accidentally process restricted records.

## Pseudonymization: Vault Transit encryption

GDPR Article 25 recommends **pseudonymization** as a technical measure for data protection by design. Granit implements this through HashiCorp Vault's Transit secret engine via `ITransitEncryptionService`.

The Transit engine performs encryption and decryption without ever exposing the encryption key to the application. The key lives in Vault. The application sends plaintext, receives ciphertext, and stores the ciphertext. If the database is compromised, the attacker gets `vault:v1:AbCdEf...` -- useless without access to Vault.

```csharp title="SensitiveDataService.cs"
public class SensitiveDataService(ITransitEncryptionService transit)
{
    public async Task<string> ProtectSsnAsync(
        string ssn, CancellationToken cancellationToken)
    {
        return await transit.EncryptAsync(
            "pii-data", ssn, cancellationToken).ConfigureAwait(false);
        // Returns: "vault:v1:AbCdEf..."
    }

    public async Task<string> RevealSsnAsync(
        string encryptedSsn, CancellationToken cancellationToken)
    {
        return await transit.DecryptAsync(
            "pii-data", encryptedSsn, cancellationToken).ConfigureAwait(false);
    }
}
```

**Key rotation** happens in Vault, not in application code. You rotate the key in Vault, and new encryptions use the new key version. Old ciphertexts remain decryptable because Vault retains previous key versions. You can also re-encrypt existing ciphertexts with the new key version via a batch operation -- no application downtime, no data migration.

For local development, `GranitVaultModule` is automatically disabled in `Development` environments. The application falls back to the local AES-256-CBC provider from `Granit.Encryption`, so developers do not need a Vault server on their machine.

The `IStringEncryptionService` abstraction supports provider switching at runtime via configuration:

| Provider | Config value | Backend |
|----------|-------------|---------|
| AES (default) | `"Aes"` | Local AES-256-CBC with PBKDF2 key derivation |
| Vault Transit | `"Vault"` | HashiCorp Vault Transit engine |

This separation means the same code works in development (AES) and production (Vault) without conditional logic.

## Audit trail: proving what happened

GDPR Articles 5(2) and 24 require **accountability** -- you must be able to demonstrate compliance. ISO 27001 Annex A.8.15 requires logging of user activities. Granit implements this at two levels.

**Entity-level audit** via `AuditedEntityInterceptor`. Every entity that extends `AuditedEntity` gets automatic tracking of `CreatedBy`, `CreatedAt`, `ModifiedBy`, and `ModifiedAt`. The interceptor resolves the current user from `ICurrentUserService` and the timestamp from `IClock` (never `DateTime.UtcNow`). These fields are set on every `SaveChanges` call -- the developer cannot forget, and the developer cannot forge the timestamp.

| Entity state | Interceptor action |
|-------------|-------------------|
| `Added` | Sets `CreatedAt`, `CreatedBy`. Auto-generates `Id`. Injects `TenantId`. |
| `Modified` | Protects `CreatedAt`/`CreatedBy` from overwrite. Sets `ModifiedAt`, `ModifiedBy`. |

**Event-level audit** via `Granit.Timeline`. While entity-level audit tells you _what_ changed and _when_, the timeline tells you _why_. Timeline entries capture business events: "Patient consent withdrawn", "Document approved", "Export requested". These entries form the audit trail that an ISO 27001 auditor can review without querying raw database tables.

The combination of entity audit (automatic, structural) and timeline (explicit, semantic) gives you both the technical proof and the business narrative required for compliance.

## Tenant isolation: data cannot leak

Multi-tenancy is a GDPR concern. If Tenant A's data appears in Tenant B's query results, that is a data breach -- full stop.

Granit enforces tenant isolation through a global query filter on all `IMultiTenant` entities. The filter expression `e.TenantId == currentTenant.Id` is applied by `ApplyGranitConventions` and evaluated on every query. There is no way to accidentally query across tenants without explicitly disabling the filter.

`Granit.BlobStorage` takes this further. When blob storage is configured with multi-tenancy, it **throws an exception** if no tenant context is available. You cannot upload or download a file without an active tenant. This is a deliberate design choice: in a GDPR context, it is better to fail loudly than to store a file without tenant attribution.

```csharp title="Tenant isolation is structural, not optional"
// This entity is automatically filtered by TenantId -- no manual WHERE clause.
public class MedicalRecord : AuditedEntity, IMultiTenant, ISoftDeletable
{
    public Guid? TenantId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    // ...
}
```

The `ICurrentTenant` abstraction uses a null object pattern (`NullTenantContext` with `IsAvailable = false`) when multi-tenancy is not installed. Modules check `IsAvailable` before using `Id`, which means the same code works in single-tenant and multi-tenant deployments without conditional branching.

## Cookie consent: no undeclared cookies

GDPR requires informed consent for non-essential cookies. Most frameworks leave cookie management to the application. Granit inverts this with a **strict registry pattern** in `Granit.Http.Cookies`.

Every cookie must be declared at startup with its category, retention period, and purpose. Writing an undeclared cookie throws `UnregisteredCookieException` when strict mode is enabled. This means a developer cannot silently introduce a tracking cookie -- the framework rejects it at runtime.

```csharp title="Cookie registration — compile-time documentation of all cookies"
cookies.RegisterCookie(new CookieDefinition(
    Name: "session_id",
    Category: CookieCategory.StrictlyNecessary,
    RetentionDays: 1,
    IsHttpOnly: true,
    Purpose: "Session identification"));
```

The `Granit.Http.Cookies.Klaro` package integrates with the Klaro consent management platform (EU-sovereign, open-source) to enforce consent before setting non-essential cookies. Four categories map directly to GDPR requirements:

| Category | Consent required | Examples |
|----------|-----------------|----------|
| `StrictlyNecessary` | No | Session, CSRF, authentication |
| `Functionality` | Yes | Preferences, language |
| `Analytics` | Yes | Usage tracking |
| `Marketing` | Yes | Advertising, retargeting |

The cookie registry doubles as living documentation. During an audit, you can enumerate every cookie your application sets, its purpose, its retention period, and its consent category -- programmatically.

## The pattern: framework constraints over application discipline

Every privacy feature described above shares a common design principle: **make the right thing automatic and the wrong thing difficult.**

- Soft delete is an interceptor, not a convention. You cannot forget it.
- Query filters are applied by `ApplyGranitConventions`, not by individual queries. You cannot skip them.
- Audit fields are set by an interceptor, not by application code. You cannot forge them.
- Tenant isolation is a query filter, not a `WHERE` clause. You cannot omit it.
- Cookie registration is enforced at runtime, not documented in a wiki. You cannot bypass it.

GDPR compliance is not achieved by training developers to remember the rules. It is achieved by building a framework where the rules are the default behavior, and violating them requires explicit, auditable action.

## Further reading

- [Granit.Privacy module reference](/dotnet/security/privacy/) -- data export saga, deletion orchestration, legal agreements
- [Granit.Vault & Encryption reference](/dotnet/security/vault-encryption/) -- Transit encryption, dynamic credentials, provider switching
- [Granit.Persistence reference](/dotnet/data/persistence/) -- interceptors, soft delete, query filters, audit trail
- [Security model](/dotnet/concepts/security-model/) -- authentication, authorization, and compliance architecture
