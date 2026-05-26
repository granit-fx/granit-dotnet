# Granit.Privacy.BlobStorage

Bridges `Granit.Privacy` personal-data exports to `Granit.BlobStorage`. Ships both
sides of the scatter-gather pipeline:

- **`PrivacyFragmentUploader`** — provider-side: every `IPrivacyDataProvider`
  Wolverine handler forwards here to run the presigned-upload dance and publish
  `PersonalDataPreparedEto`.
- **`ExportArchiveAssemblyHandler`** — terminal: consumes `ExportCompletedEto`,
  streams every fragment into a ZIP archive (temp file, `CountingStream`-guarded
  for `ExportMaxSizeMb`), writes `manifest.json`, uploads, and marks the
  `IExportRequestTrackerWriter` as `Completed` / `PartiallyCompleted` /
  `SizeLimitExceeded`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Privacy.BlobStorage
```

## Dependencies

- `Granit.Privacy`
- `Granit.BlobStorage`

## Usage

Add the module to your host application:

```csharp
[DependsOn(typeof(GranitPrivacyBlobStorageModule))]
public class MyAppModule : GranitModule { }
```

Per-module provider handlers (e.g. in `Granit.Identity.Local.Privacy`) forward to
the uploader in one line:

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

The uploader:

1. Calls `provider.ExportAsync(userId, ct)` to obtain the fragment bytes
2. For empty fragments — publishes `PersonalDataPreparedEto` with the
   `empty:{RequestId}` sentinel (see `PrivacyExportContainerNames.EmptyFragmentPrefix`).
   No blob is created for empty providers.
3. Otherwise — runs the presigned upload dance
   (`InitiateUploadAsync` → HTTP PUT → `ConfirmUploadAsync`) and publishes
   `PersonalDataPreparedEto` with the confirmed blob id

### Download endpoint

```csharp
app.MapGranitPrivacy();               // from Granit.Privacy.Endpoints
app.MapGranitPrivacyExportDownload(); // from this package
```

Exposes `GET /privacy/exports/{requestId}/download` which resolves the tracker,
verifies the caller owns the request, and 302-redirects to a short-lived presigned
URL of the assembled ZIP. Kept in this package (not `Granit.Privacy.Endpoints`) so
the core privacy endpoints stay usable without a BlobStorage dependency.

### Assembler flow

The archive assembler:

1. Receives `ExportCompletedEto` with the full fragment list
2. For each non-`empty:` fragment: requests a presigned download URL (TTL from
   `GranitPrivacyOptions.ArchiveAssemblyDownloadUrlExpiryMinutes`, default 15 min),
   streams the bytes straight into a ZIP entry
3. For `empty:` sentinels: records the provider under `manifest.EmptyProviders`
   without calling `CreateDownloadUrlAsync`
4. Writes `manifest.json` as the final entry
5. Uploads the ZIP, confirms it, and calls `MarkCompletedAsync` with either
   `Completed`, `PartiallyCompleted` (saga timed out), or `SizeLimitExceeded`
   (archive exceeded `ExportMaxSizeMb`)

## Bucket lifecycle policies

Personal-data exports are persistent objects under GDPR Art. 12(3) and need an
explicit retention ceiling on the storage account. The framework does NOT
auto-delete on its own — host operators configure the lifecycle policy to
match their compliance posture. Reference Terraform / Bicep snippets for S3,
Azure Blob, and GCS, plus the planned tag-based completion lifecycle, live in
[Lifecycle.md](Lifecycle.md).

## Documentation

See the [full documentation](https://granit-fx.dev).
