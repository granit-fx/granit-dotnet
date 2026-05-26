# Bucket lifecycle policies for personal-data exports

This package writes two classes of objects into blob storage:

| Object | Container / bucket | Lifetime |
| --- | --- | --- |
| Shard ZIPs (`personal-data-export/{requestId}-{NNN}.zip`) | `gdpr-exports` (`PrivacyExportContainerNames.FragmentContainer`) | Long enough for the data subject to download — but no longer. |
| Manifest sidecar (`personal-data-export-{requestId}-manifest.json`) | same bucket | Same as shards. |
| Staged provider fragments (transient JSON / blobs uploaded by `PrivacyFragmentUploader`) | same bucket | Until the assembly job has consumed them. |

The assembly path leaves an object behind for two reasons:

- **Crash-resume.** A failed multipart upload sits as part-blobs until the lifecycle policy reaps them.
- **GDPR Art. 12(3) ceiling.** A completed export should not linger indefinitely — the controller MUST delete it once the subject has had a reasonable window to download.

The framework intentionally does NOT auto-delete on its own — host operators configure the storage account's lifecycle policy to match their compliance posture. The reference policies below are the framework's defaults.

## S3 (Terraform)

```hcl
resource "aws_s3_bucket_lifecycle_configuration" "gdpr_exports" {
  bucket = aws_s3_bucket.gdpr_exports.id

  # 1. Reap orphan multipart uploads — assemble crashes, AbortMultipartUpload
  # never fires, the parts sit forever otherwise. 1 day is well past Wolverine's
  # default retry-with-cooldown budget (~21 minutes).
  rule {
    id     = "abort-incomplete-multipart"
    status = "Enabled"
    abort_incomplete_multipart_upload {
      days_after_initiation = 1
    }
  }

  # 2. Safety-net expiration — every object in the bucket expires after the
  # configured GDPR retention window, regardless of whether the assembler
  # tagged it as completed. Defends against a crash between
  # CompleteMultipartUpload and the (future) tag-on-completion call.
  rule {
    id     = "gdpr-export-retention-safety-net"
    status = "Enabled"
    expiration {
      days = 30
    }
  }
}
```

## Azure Blob (Bicep)

```bicep
resource lifecyclePolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          name: 'gdpr-export-retention-safety-net'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: ['blockBlob']
              prefixMatch: ['gdpr-exports/']
            }
            actions: {
              baseBlob: {
                delete: { daysAfterCreationGreaterThan: 30 }
              }
            }
          }
        }
      ]
    }
  }
}
```

Azure Block Blob has no equivalent of S3's `abort_incomplete_multipart_upload` — Azure documents that uncommitted blocks expire **automatically after 7 days** per the service contract, so the rule isn't needed.

## GCS (Terraform)

```hcl
resource "google_storage_bucket" "gdpr_exports" {
  name                        = "gdpr-exports"
  uniform_bucket_level_access = true

  lifecycle_rule {
    condition { age = 30 }
    action    { type = "Delete" }
  }

  # GCS resumable upload sessions expire after 7 days per the service contract,
  # so no explicit abort-incomplete rule is required.
}
```

## Tunables

The framework's defaults assume a 30-day grace window:

- **Too short** for a subject who fired and forgot the request — the email link will 404 silently.
- **Too long** wastes storage and increases the breach-blast radius.

30 days mirrors the manifest expiry encoded in `ExportDownloadUrlExpiryMinutes` (24 h presigned URL) × a 30× safety margin, and aligns with most regulators' "month or so" reasonable-window expectations under Art. 12(3).

## Tag-based completion lifecycle (planned)

The plan §14 calls for a more granular policy that distinguishes completed objects from in-flight ones:

- Reap **uncompleted** uploads after 1 day.
- Reap **completed** shards after 30 days — keyed off a `granit-export-status=completed` object tag the assembler would set on `CompleteMultipartUploadAsync`.

This requires extending `IBlobStoreProvider` with a tagging hook on multipart completion. Tracked as a follow-up; the age-based policy above is the working interim.
