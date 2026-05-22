# Granit.Presence

User presence and availability for Granit. Tracks online/away/offline status from a client
heartbeat and persists a manual override (`Available`, `Busy`, `DoNotDisturb`,
`AppearOffline`) with optional expiration.

## What it ships

- `PresenceStatus` enum (Online / Away / Offline / Busy / DoNotDisturb).
- `UserPresence` aggregate carrying the manual override (no `IMultiTenant` — presence is
  global per user).
- `IPresenceTracker` with a FusionCache-backed default impl applying the multi-tab MAX
  anti-flapping rule.
- `IPresenceQueryService` blending override + heartbeat into a `PresenceSnapshot`.
- `IPresenceHeartbeatRecorder` and `IPresenceOverrideService` orchestrating mutations
  and publishing `UserPresenceChangedEto`.
- `Granit.Presence` ActivitySource + meter.

## Production storage

Default registrations are in-memory. For durability add
`Granit.Presence.EntityFrameworkCore`. For cross-pod heartbeat propagation add
`Granit.Caching.StackExchangeRedis` — the FusionCache backplane will broadcast presence
updates between hosts.

## DnD-aware notifications

Add `Granit.Presence.Notifications` to suppress push channels (`SignalR`, `Sse`,
`WebPush`, `MobilePush`) when the user is in `DoNotDisturb` or `Offline`. Store-and-forward
channels (`InApp`, `Email`, `Sms`) keep delivering.
