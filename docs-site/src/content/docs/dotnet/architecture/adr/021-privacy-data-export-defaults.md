---
title: "ADR-021: Privacy Data Export — Framework Defaults"
description: "The framework ships an end-to-end default implementation of the personal-data export pipeline: EF Core tracker persistence, IPrivacyDataProvider contract, streaming ZIP archive assembler, module-owned providers, and a presigned download endpoint. Apps opt into built-ins and only write a provider for data not covered by a module."
sidebar:
  order: 21
  label: "021 - Privacy Data Export Defaults"
---

> **Date:** 2026-04-19
> **Authors:** Jean-Francois Meyers
> **Scope:** `granit-dotnet` (`Granit.Privacy`, `Granit.Privacy.BlobStorage`,
> `Granit.Privacy.EntityFrameworkCore`, `Granit.Identity.Local.Privacy`,
> `Granit.Auditing.Privacy`, `Granit.Notifications.Privacy`)

## Context

Feature #553 / Story #554 landed the scatter-gather export saga
(`PersonalDataExportSaga`) and the HTTP endpoints (`POST /privacy/exports`,
`GET /privacy/exports/{id}`) but stopped short of a working end-to-end flow:

- `IExportRequestTrackerReader` / `IExportRequestTrackerWriter` shipped as
  **contracts only** — no implementation, anywhere in the monorepo. Booting any
  host that mapped the privacy endpoints crashed with *"No service for type
  'IExportRequestTrackerReader' has been registered"*.
- The archive referenced by `ExportCompletedEto.ArchiveBlobReferenceId` was
  documented in the saga's XML doc but **never assembled** — no code in the
  framework zipped fragments, uploaded the archive, or served it back.
- **No `IPrivacyDataProvider` contract** existed. Each app had to hand-write a
  Wolverine handler that re-implemented the presigned-upload dance (initiate →
  HTTP PUT → confirm → publish `PersonalDataPreparedEto`), inventing its own
  conventions for file naming, content type, and empty-user handling.
- Granit modules that clearly own personal data (`Identity.Local`, `Auditing`,
  `Notifications`) shipped no built-in provider, so a compliant app had to
  duplicate reader-over-DbContext logic in its own codebase — and keep it in
  sync whenever a module schema changed.

The practical result: "GDPR export" was in the marketing copy but not in the
framework. Showcase worked around it with a bespoke `PersonalDataExportHandler`
in `Showcase.Modules.Security`, which is exactly the copy-paste the framework
was supposed to prevent.

## Decision

The framework provides a turnkey personal-data export pipeline. Apps opt in,
register their module-specific providers (almost always one-liners), and the
end-to-end flow — tracking, fragment upload, archive assembly, manifest,
presigned download — is handled by shared code.

### 1. Static-abstract `IPrivacyDataProvider` contract

Providers implement a minimal, allocation-free contract:

```csharp
public interface IPrivacyDataProvider
{
    static abstract string ProviderName { get; }
    static abstract string ContentType { get; }
    static abstract string FileName(Guid requestId);
    Task<ReadOnlyMemory<byte>> ExportAsync(Guid userId, CancellationToken ct);
}
```

The static-abstract members let the uploader and the registry reason about the
provider without instantiating it. Returning `ReadOnlyMemory<byte>.Empty`
signals "the user has no data here" — the framework emits the
`empty:{RequestId}` sentinel fragment and records the provider in the archive
manifest's `EmptyProviders` list rather than uploading an empty blob.

### 2. `PrivacyFragmentUploader` utility (provider-side)

The presigned-upload boilerplate (`InitiateUploadAsync` → HTTP PUT →
`ConfirmUploadAsync` → `PublishAsync(PersonalDataPreparedEto)`) lives once, in
`Granit.Privacy.BlobStorage`. Every per-module Wolverine handler becomes a
one-line forwarder:

```csharp
public class IdentityLocalPersonalDataExportHandler
{
    public static Task HandleAsync(
        PersonalDataRequestedEto request,
        IdentityLocalPrivacyDataProvider provider,
        PrivacyFragmentUploader uploader,
        CancellationToken ct) =>
        uploader.UploadAsync(request, provider, ct);
}
```

### 3. Streaming ZIP `ExportArchiveAssemblyHandler` (terminal)

Consumes `ExportCompletedEto`, streams every fragment into a ZIP archive via a
temp file, writes `manifest.json` as the final entry, uploads through the
presigned dance, and marks the tracker:

- **No `byte[]` buffering.** A `CountingStream` decorator enforces
  `GranitPrivacyOptions.ExportMaxSizeMb` *during* the stream copy — a post-hoc
  check would defeat the purpose. Exceeding the cap transitions the request to
  `ExportRequestState.SizeLimitExceeded`.
- **15-minute presigned download TTL** for fragments
  (`ArchiveAssemblyDownloadUrlExpiryMinutes`) — the assembler fires at the end
  of the saga, which can be up to `ExportTimeoutMinutes` + Wolverine retry
  budget after the earliest fragment was uploaded.
- **Empty-fragment recognition**: fragments starting with `empty:` are recorded
  in `manifest.EmptyProviders` and never dereferenced (passing the sentinel to
  `CreateDownloadUrlAsync` would raise `BlobNotFoundException`).

### 4. EF Core tracker as the default

`Granit.Privacy.EntityFrameworkCore.UseEntityFrameworkCoreTrackers()` registers
both export and deletion trackers against the existing `PrivacyDbContext`. No
custom store is needed for the typical case; apps keep the extension point
(`UseExportRequestTracker<T>`) for bespoke persistence.

### 5. Module-owned providers

The modules that own personal data ship their own providers as separate
packages under the existing `.Wolverine` sub-pattern:

| Module | Package | Provider | Fragment |
| ------ | ------- | -------- | -------- |
| `Identity.Local` | `.Wolverine` | `identity-local` | Profile + roles |
| `Auditing` | `.Wolverine` | `auditing` | User audit trail (capped) |
| `Notifications` | `.Privacy.Wolverine` | `notifications` | Inbox + prefs |

The `Notifications` provider lives in a dedicated package (rather than
`Granit.Notifications.Wolverine`, which exists for durable dispatch) so
consumers of the durable-dispatch integration do not inherit a Privacy +
BlobStorage dependency.

### 6. Presigned download endpoint

`MapGranitPrivacyExportDownload()` (in `Granit.Privacy.BlobStorage`) mounts
`GET /privacy/exports/{id}/download`, validates ownership + state, and 302s to
a short-lived presigned URL. Lives in the BlobStorage-dependent package so the
core `Granit.Privacy.Endpoints` package stays usable without BlobStorage.

### 7. Pairing invariant as an architecture test

`PrivacyDataProviderConventionTests` enforces that every non-abstract
`IPrivacyDataProvider` has a matching `*PersonalDataExportHandler` in the same
assembly forwarding `PersonalDataRequestedEto` through `PrivacyFragmentUploader`.
Without the pairing, a provider is registered with the scatter-gather registry
but never produces a fragment — the saga silently times out at
`ExportTimeoutMinutes`. The test catches the omission at build time.

## Consequences

### Positive

- New apps get a working export flow with a single
  `AddGranitPrivacy(p => p.UseEntityFrameworkCoreTrackers()...)` call —
  zero custom code.
- Writing a provider for a custom module is ~20 lines of
  `IPrivacyDataProvider` plus a one-line handler.
- Size, TTL, and empty-fragment edge cases are handled once, consistently.
- Privacy endpoints stay independent of BlobStorage; BlobStorage-dependent
  features (archive + download) ship in a package consumers opt into.
- The pairing arch-test prevents the silent-timeout class of regression.

### Negative

- Four new packages to track in `Directory.Packages.props` across downstream repos.
- `IAuditingReader` gains a `GetByUserAsync(string, int, ct)` method — a small
  breaking change for custom implementations (existing NSubstitute mocks are
  unaffected thanks to default stubs).
- The 10 000-entry cap on `auditing` and 5 000-entry cap on `notifications` are
  convenience defaults, not a guarantee — users with larger footprints receive
  a `Truncated`-flagged partial export. Making the caps configurable is
  intentionally deferred to a follow-up (YAGNI until a real tenant pushes past
  them).

## Alternatives considered

- **Abstract `PrivacyDataProviderHandlerBase<T>` base class** instead of a
  utility service. Rejected: CLAUDE.md mandates handlers be `public class`
  (non-static) with `public static Handle` methods so Wolverine's assembly
  scanner picks them up without a `[WolverineHandler]` attribute that would
  create a hard WolverineFx dependency. A base class forces inheritance across
  assemblies; a utility service composes cleanly into a one-line handler.
- **Bundle the archive handler in `Granit.Privacy.BackgroundJobs`.** The
  earlier plan put it there, but that package's raison d'être is the deletion
  deadline enforcer — forcing a BlobStorage dependency on apps that use it
  only for deletion was the wrong trade. The handler is inherently
  BlobStorage-specific; it belongs in `Granit.Privacy.BlobStorage`.
- **`byte[]`-based archive assembly.** An in-memory `ZipArchive` is simpler but
  OOM-risks the worker for active users with large auditing trails
  (100+ MB payloads are plausible under ISO 27001 retention rules).
- **Post-assembly size check** instead of `CountingStream`. Rejected for the
  same OOM reason: you can't check a byte count without materializing.
- **Reuse an existing `Granit.Notifications.Wolverine` package for the
  notifications provider.** Rejected: that package's current purpose (durable
  dispatch) is orthogonal, and adding Privacy + BlobStorage transitively would
  pollute every consumer.

## References

- Feature #1049 — *Complete data export pipeline* (tracker EF + streaming
  archive + module providers), epic #79.
- Stories #1050 (tracker EF + uploader), #1051 (module providers), #1052
  (download endpoint).
- [Granit.Privacy.BlobStorage README](https://github.com/granit-fx/granit-dotnet/blob/develop/src/Granit.Privacy.BlobStorage/README.md)
- [`data-export.mdx`](../../compliance/privacy/data-export.mdx)
