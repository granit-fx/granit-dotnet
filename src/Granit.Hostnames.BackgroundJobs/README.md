# Granit.Hostnames.BackgroundJobs

Recurring DNS verification poller for `Granit.Hostnames`.

## What it does

`VerifyHostnamesJob` runs every 5 minutes and checks hostnames in `Verifying` or `Error` state
whose `NextCheckAt ≤ now`. For each due hostname the `IHostnameVerifier` queries live DNS and:

- **All expected records match** → `MarkVerified()` → `Active` + `HostnameVerifiedEto`
- **Mismatch / timeout** → `MarkFailed(conflicts)` → `Error` + exponential backoff + `HostnameVerificationFailedEto`

After `ManagedHostname.DormancyThreshold` consecutive failures `NextCheckAt` is set to `null` and
the domain stops being picked up automatically. An admin must call `POST /hostnames/{id}/verify-now`
to restart.

## Registration

```csharp
builder.AddGranitModule<GranitHostnamesBackgroundJobsModule>();
```

Requires an `IHostnameVerifier` registration (provided by the built-in DNS implementation or a
provider adapter such as `Granit.Hostnames.Cloudflare`).
