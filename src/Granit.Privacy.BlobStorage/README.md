# Granit.Privacy.BlobStorage

Bridges `Granit.Privacy` personal-data exports to `Granit.BlobStorage`. Ships
`PrivacyFragmentUploader`, the shared presigned-upload + event-publication utility
used by every `IPrivacyDataProvider` Wolverine handler in the scatter-gather saga.

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

Per-module provider handlers (e.g. in `Granit.Identity.Local.Wolverine`) forward to
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
4. The archive assembler in `Granit.Privacy.BackgroundJobs` consumes the sentinel
   to record the provider under `manifest.EmptyProviders` instead of downloading
   a non-existent blob

## Documentation

See the [full documentation](https://granit-fx.dev).
