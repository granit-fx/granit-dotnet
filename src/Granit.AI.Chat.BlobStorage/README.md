# Granit.AI.Chat.BlobStorage

Wires `Granit.BlobStorage` as the [`IAIAttachmentSource`](../Granit.AI.Chat/Attachments/IAIAttachmentSource.cs)
for `Granit.AI.Chat` (ADR-067). Attachment references are resolved as validated blob bytes via
`IBlobContentReader`, then injected as untrusted document context in the AI turn.

## Usage

Add the module to your host application:

```csharp
[DependsOn(typeof(GranitAIChatBlobStorageModule))]
public class MyAppModule : GranitModule { }
```

Or register the attachment source directly:

```csharp
services.AddBlobStorageChatAttachments();
// With optional size/type overrides:
services.AddBlobStorageChatAttachments(o => o.MaxAttachmentBytes = 5 * 1024 * 1024);
```

## Expected reference format

The `AttachmentRequest.Reference` must be the **blobId (Guid string)** returned by the
BlobStorage upload flow:

```
POST /api/blob-storage/{container}/upload       → PresignedUploadTicket (blobId + uploadUrl)
PUT  {uploadUrl}                                → direct client-to-cloud transfer
POST /api/blob-storage/{container}/confirm/{id} → validates the blob (Status → Valid)
```

Only blobs in `Valid` state are resolved. Absent, pending, rejected, or cross-tenant blobs
return `null` — the attachment is silently dropped, nothing leaks into the prompt.

## How it works

`BlobStorageAIAttachmentSource` parses the reference as a `Guid`, calls `IBlobContentReader.ReadAsync`,
and maps the result to `AIAttachmentData`. `IBlobContentReader` is a public interface registered in
`GranitBlobStorageModule` — implementable by any consumer without `InternalsVisibleTo`.

Tenant ACL is implicit: `IBlobDescriptorReader` scopes reads to the current tenant automatically.

## Documentation

See the [full documentation](https://granit-fx.dev).
