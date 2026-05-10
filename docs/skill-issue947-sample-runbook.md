# Skill: Run Issue947 Notification Verifier Reliably

## Purpose
Run `samples/Issue947.NotificationDeliveryVerify` and quickly diagnose failures around SDK/tooling/repo policies.

## Preconditions
1. Repo uses `.NET 10` (`global.json` pins `10.0.103`).
2. Prefer containerized run when host SDK is missing:
   - `podman run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 ...`

## Standard Run Command
```bash
dotnet run --project samples/Issue947.NotificationDeliveryVerify/Issue947.NotificationDeliveryVerify.csproj
```

Container form:
```bash
podman run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet run --project samples/Issue947.NotificationDeliveryVerify/Issue947.NotificationDeliveryVerify.csproj
```

## Failure Map (What to Fix)

### 1) SDK mismatch
- Symptom: `A compatible .NET SDK was not found` with `Requested SDK version: 10.0.103`
- Fix: run in `mcr.microsoft.com/dotnet/sdk:10.0` container or install .NET 10 locally.

### 2) Central Package Management (NU1010)
- Symptom: `PackageReference ... do not define a corresponding PackageVersion`
- Cause: sample references a package not listed in `Directory.Packages.props`.
- Fix options:
  1. Add package to `Directory.Packages.props`, or
  2. Use `VersionOverride` in sample csproj (localized fix).

### 3) Missing namespace/type
- Symptom: `NotificationDeliveryContext could not be found`
- Fix: add `using Granit.Notifications;`.

### 4) Host disposal mismatch
- Symptom: `IHost ... must implement IAsyncDisposable`
- Fix: replace `await using IHost host = ...` with `using IHost host = ...`.

### 5) Granit analyzers blocking sample
- Typical diagnostics:
  - `GRSEC001` (avoid `DateTimeOffset.UtcNow`)
  - `GRSEC002` (avoid `Guid.NewGuid()`)
  - `RS0030` (avoid `Console.WriteLine`)
  - `EF1001` (internal EF API use)
- Fix options:
  1. Make sample fully compliant (ILogger + IClock + IGuidGenerator), or
  2. Suppress in sample csproj via `NoWarn` when sample is intentionally minimal.

### 6) DI missing dependencies
- Symptom: `Unable to resolve service ... ICurrentTenant`
- Fix: register minimal test stub for `ICurrentTenant` in sample host.

## Expected Success Signal
Process exits `0` and prints:
`Email sends observed (expected 1): 1`

## Commit Hygiene
If the request is “commit non-sample changes only”:
1. Unstage sample files:
   - `git restore --staged samples/Issue947.NotificationDeliveryVerify/*`
2. Commit only `src/`, `tests/`, `docs/` as requested.
