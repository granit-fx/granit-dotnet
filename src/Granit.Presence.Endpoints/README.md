# Granit.Presence.Endpoints

Minimal API endpoints for `Granit.Presence`: self-managed manual override, polling
heartbeat, and batch presence query. Protected by `Presence.Self.*` and `Presence.Users.*`
permissions.

## Routes

```text
PUT    /api/v1/presence/my                  # set override (Presence.Self.Manage)
DELETE /api/v1/presence/my/override         # clear override (Presence.Self.Manage)
GET    /api/v1/presence/my                  # my snapshot
POST   /api/v1/presence/my/poll             # heartbeat (idleSeconds)
GET    /api/v1/presence/users/{userId}      # one user (Presence.Users.Read)
POST   /api/v1/presence/users/batch         # N users (Presence.Users.Read)
```

## Heartbeat protocol

Clients send `{ "idleSeconds": 45 }` every 30–60s — no timestamp because browser clocks
are unreliable. The server reconstructs `LastActivityUtc = clock.UtcNow - idleSeconds`
and applies a MAX merge rule across concurrent tabs.
