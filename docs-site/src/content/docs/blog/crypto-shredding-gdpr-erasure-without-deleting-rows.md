---
title: "Crypto-Shredding: GDPR Erasure Without Deleting a Single Row"
date: 2026-03-24
sidebar:
  order: 12
authors:
  - jfmeyers
tags:
  - security
  - compliance
  - deep-dive
description: "Destroy the encryption key, not the data. How Granit implements GDPR Art. 17 crypto-shredding with per-entity key isolation — one API call, permanent erasure, zero rows deleted."
excerpt: >
  Physical deletion breaks audit trails, conflicts with legal holds, and fails on
  append-only systems. Crypto-shredding solves all three: destroy the key, and the
  ciphertext becomes random noise. Here is how Granit implements it.
---

Your DPO sends you a ticket: "Patient #4821 exercised their right to erasure. All personal data must be irreversibly deleted within 30 days." You open your ORM, write a `DELETE`, and ship it.

Six months later, during an ISO 27001 audit, someone asks: "Where is the audit trail for patient #4821?" You stare at the screen. The row is gone. The foreign keys cascaded. The timeline entries that referenced this patient now point to nothing. The auditor writes a finding.

**You satisfied one regulation by violating another.**

This is not a hypothetical edge case. It is the default outcome when you treat GDPR erasure as a SQL `DELETE`. In this post, I will explain why physical deletion is architecturally hostile to compliance, and how **crypto-shredding** — destroying encryption keys instead of data — resolves the contradiction.

## The impossible triangle

GDPR Article 17 demands **erasure**. ISO 27001 demands **immutable audit trails**. Your legal team demands **data retention** during active litigation. You cannot satisfy all three with a `DELETE` statement.

| Obligation | What it requires | What `DELETE` does |
| ---------- | ---------------- | ------------------ |
| GDPR Art. 17 | Irreversible erasure of personal data | Satisfies (row gone) |
| ISO 27001 A.8.15 | Immutable, traceable audit trail | **Violates** (evidence destroyed) |
| Legal hold | Retain data until litigation ends | **Violates** (data gone) |
| Backup consistency | Erasure must cover all copies | **Fails** (backup still has it) |

Soft delete (`IsDeleted = true`) is not erasure — an admin can still read the data, and regulators know it. Anonymization (replacing PII with "REDACTED") works in some cases but fails when backups retain the original values, and the replacement quality is hard to verify.

**Crypto-shredding** breaks the triangle by redefining what "erasure" means. If data is encrypted with a unique key and you permanently destroy that key, the ciphertext is mathematically equivalent to random noise. The row stays. The audit trail stays. The foreign keys stay. But the personal data is gone — permanently, provably, irreversibly.

The **European Data Protection Board** (Guidelines 5/2019), the **UK ICO**, and the **French CNIL** all recognize this as valid erasure, provided the encryption is strong (AES-256), the key destruction is irreversible, and the destruction is auditable.

## The key insight: one key per entity

Standard field-level encryption uses a **shared key ring** — the same key encrypts every patient's SSN. Destroying that key would make every patient's data unreadable. That is not targeted erasure. That is a catastrophe.

Crypto-shredding requires **per-entity key isolation**: each entity instance gets its own encryption key. When patient #4821 exercises their right to erasure, you destroy only their key. Every other patient's data remains perfectly readable.

In Granit, this is a single attribute:

```csharp title="Patient.cs"
public sealed class Patient : AuditedAggregateRoot
{
    public string LastName { get; private set; } = string.Empty;

    [Encrypted]
    public string NationalId { get; private set; } = string.Empty;       // shared key

    [Encrypted(KeyIsolation = true)]
    public string MedicalNotes { get; private set; } = string.Empty;     // per-entity key

    [Encrypted(KeyIsolation = true)]
    public string? DiagnosisCode { get; private set; }                   // per-entity key
}
```

`NationalId` uses shared encryption — fast, simple, supports key rotation via `IReEncryptionJob`. `MedicalNotes` and `DiagnosisCode` use isolated keys — each patient has their own AES-256 key stored in HashiCorp Vault KV v2. When you shred patient #4821, only their medical notes and diagnosis become unreadable. Their `NationalId` (shared key) and `LastName` (plaintext) are unaffected.

**Use key isolation only for properties that need individual erasure.** It is heavier than shared encryption — one Vault KV secret per entity instance. Choose deliberately.

## How it works under the hood

The technical challenge is subtle. EF Core value converters receive a scalar value — they have no access to the entity type or ID. But per-entity key isolation needs `{EntityType}:{EntityId}` to look up the right key.

Granit solves this with **two EF Core interceptors**, not a value converter:

**On write** (`EncryptionIsolationSaveChangesInterceptor`): iterates the change tracker, finds entities with `[Encrypted(KeyIsolation = true)]` properties, calls `IEntityEncryptionKeyStore.GetOrCreateKeyAsync(entityType, entityId)` to get (or generate) the per-entity AES-256 key, encrypts in-place, and saves.

**On read** (`EncryptionIsolationMaterializationInterceptor`): after EF Core materializes an entity, checks for isolated properties, retrieves the key from the store, and decrypts in-place. If the key is gone (shredded), the property is set to `null`.

The interceptors must run **after** `AuditedEntityInterceptor` (which assigns entity IDs). One line in your `DbContext` setup ensures correct ordering:

```csharp title="AppDbContext setup"
options.UseGranitInterceptors(sp);              // audit, versioning, soft delete
options.UseGranitEncryptionInterceptors(sp);    // encryption isolation (must be after)
```

## Shredding in practice

When the Privacy module's deletion saga reaches its deadline, it publishes `PersonalDataDeletionRequestedEto`. Your data provider handler catches it and calls `ICryptoShredder`:

```csharp title="PatientDeletionHandler.cs"
public sealed class PatientDeletionHandler(ICryptoShredder shredder)
{
    public async Task<PersonalDataDeletedEto> HandleAsync(
        PersonalDataDeletionRequestedEto request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        List<string> patientIds = await db.Patients
            .Where(p => p.CreatedBy == request.UserId.ToString())
            .Select(p => p.Id.ToString())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        await shredder.ShredBatchAsync("Patient", patientIds, cancellationToken)
            .ConfigureAwait(false);

        return new PersonalDataDeletedEto(
            request.RequestId, "PatientModule",
            DeletionAction.CryptoShredding, patientIds.Count,
            DateTimeOffset.UtcNow);
    }
}
```

`ShredBatchAsync` calls `IEntityEncryptionKeyStore.DeleteKeyAsync` for each entity, which permanently destroys the Vault KV secret (metadata delete — all versions gone, no recovery). Then it notifies all registered `ICryptoShreddingAuditRecorder` implementations to write an immutable `SystemLog` entry in the Timeline.

After shredding, reading patient #4821 returns:

```csharp
var patient = await db.Patients.FindAsync([patientId], cancellationToken);

patient.MedicalNotes;   // null — key destroyed
patient.DiagnosisCode;  // null — key destroyed
patient.NationalId;     // still readable — shared key, not shredded
patient.LastName;       // still readable — not encrypted
```

The row is intact. The audit trail is intact. The compliance officer is satisfied. And the PII is mathematically gone.

## The audit trail closes the loop

ISO 27001 A.10.1.2 requires traceability of key destruction. `DefaultCryptoShredder` automatically calls all registered `ICryptoShreddingAuditRecorder` implementations after each key deletion. Granit's Timeline module provides the natural storage for these records:

```csharp title="TimelineCryptoShreddingRecorder.cs"
public sealed class TimelineCryptoShreddingRecorder(
    ITimelineWriter writer) : ICryptoShreddingAuditRecorder
{
    public async Task RecordAsync(
        string entityType, string entityId,
        DateTimeOffset shreddedAt,
        CancellationToken cancellationToken = default)
    {
        await writer.PostEntryAsync(
            entityType, entityId,
            TimelineEntryType.SystemLog,
            $"Encryption key destroyed at {shreddedAt:O}",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
```

`TimelineEntryType.SystemLog` entries are **immutable** — the Timeline module rejects deletion attempts. Even a database administrator cannot erase the evidence that key destruction occurred. The auditor finds a clean, timestamped trail.

## When to use it — and when not to

Crypto-shredding is the right choice when:

- You have **immutable storage** (event sourcing, WORM, Kafka) where physical deletion is impossible
- You need to satisfy **both erasure and retention** simultaneously (GDPR + legal hold)
- You need **granular erasure** — one patient, not the entire table
- **Backups are a concern** — ciphertext in backups is useless without the key

Skip it when:

- **Physical deletion is sufficient** for your compliance requirements — it is simpler
- The data is **not PII** — key isolation adds Vault overhead per entity
- **Anonymization** is acceptable and cheaper for your use case
- You have **very high read throughput** on encrypted fields — the materialization interceptor adds per-entity lookups (mitigated by caching, but still more than shared encryption)

## Key takeaways

- **Physical deletion satisfies GDPR but violates ISO 27001.** You need both.
- **Crypto-shredding** destroys the encryption key, not the data. The row stays, the PII is gone.
- **Per-entity key isolation** (`[Encrypted(KeyIsolation = true)]`) gives each entity its own AES-256 key in Vault KV.
- **`ICryptoShredder.ShredAsync`** destroys the key in one API call. The ciphertext becomes random noise.
- **The audit trail is automatic.** `ICryptoShreddingAuditRecorder` writes immutable `SystemLog` entries.

## Further reading

- [Crypto-Shredding module reference](/dotnet/security/crypto-shredding/) — setup, API reference, Vault KV structure
- [Field-level Encryption](/dotnet/security/encryption/) — shared key encryption, `[Encrypted]` attribute, key rotation
- [Privacy module](/dotnet/security/privacy/) — GDPR deletion saga, cooling-off period, `DeletionAction`
- [GDPR by Design](/blog/gdpr-by-design/) — broader privacy patterns in Granit
