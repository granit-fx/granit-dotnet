---
title: "ADR-049: Default landing route resolution"
description: "GET /api/me/landing-route resolves where the user lands at login via a 5-tier precedence (user-sticky > user-pinned > role-default > tenant-default > framework-default). The result is a URL/route, not just a workspace name — supports deep-link with query string (Frappe-style). URL whitelist enforced at write time. Falls through gracefully if a tier resolves to a route the user can't access."
sidebar:
  order: 49
  label: "049 - Default Landing Route"
---

> **Date:** 2026-04-30
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (`Granit.Workspaces`, `Granit.Workspaces.Endpoints`); granit-front (login redirect)
> **Epic:** [#1506](https://github.com/granit-fx/granit-dotnet/issues/1506) — Refonte UI Hybride
> **Story:** [#1528](https://github.com/granit-fx/granit-dotnet/issues/1528) — ADR Default landing route resolution
> **Status:** Accepted

## Context

After login, the React shell needs to know where to land. Three reference frameworks shape the conversation:

- **Frappe** ships role-level "default route" configuration: an admin maps a role to a target route — and the route can be a deep link with query parameters (`/app/sales-order/list?status=Draft`), not just a workspace name. This is the most flexible model — a role can land directly on the filtered list it operates on, skipping a click.
- **Odoo** lets each user pin a "Home action" pointing to a single window action. Less flexible than Frappe's route-based model; less common (most Odoo users never customize it).
- **Notion** uses last-visited as a heuristic (LocalStorage-backed). Zero configuration, works for personal-productivity scenarios; loses the role-based admin lever Frappe has.

Granit's planning conversation locked the synthesis: **5-tier precedence** combining user-pinned + role-default (from Frappe) + sticky (from Notion) + tenant-default (operator-mandated) + framework-default (last resort). Routes can be deep links with query strings (Frappe-style). All routes pass a URL whitelist before persistence.

## Decision

### 1. Five-tier precedence — top wins

```text
1. User sticky          (LocalStorage + DB sync; last workspace/route visited)
2. User pinned          (the user explicitly chose their landing — overrides sticky)
3. Role default route   (admin-configured per role; deep-link supported)
4. Tenant default route (operator-mandated tenant-wide fallback)
5. Framework default    (hardcoded "/" or first accessible workspace)
```

Each tier is **evaluated in order**; the first one that resolves to a route the user can access wins. Tiers that resolve to inaccessible routes (deleted workspace, removed permission) **fall through** transparently to the next tier — no 404 at login.

### 2. The result is a route, not a workspace name

A landing route is a URL fragment with optional query string. Examples:

- `/w/accounting` — workspace home (lands on the workspace's landing dashboard if defined per ADR-044 §7, else the section list)
- `/w/accounting/invoicing/customers` — workspace-scoped collection
- `/w/accounting/invoicing/customers?view=overdue` — workspace-scoped collection with a saved EntityView pre-selected
- `/w/accounting/invoicing/invoices?status=Draft&sort=-issuedAt` — workspace-scoped collection with ad-hoc filters
- `/customers/{id}` — workspace-agnostic detail (per ADR-044 §5)

This **route-based model**, copied from Frappe, lets a CFO role land directly on the overdue-invoice list rather than the Accounting workspace home. One click saved per session, multiplied across the user base.

### 3. Endpoint — `GET /api/me/landing-route`

```http
GET /api/me/landing-route

200 OK
{
  "route": "/w/accounting/invoicing/customers?view=overdue",
  "source": "role-default"
}
```

`source` is one of `personal-sticky` / `personal-pinned` / `role` / `tenant` / `framework`. Useful for the frontend to surface "your role default" hints in the user-prefs UI.

The endpoint is called **once at login**, before the React shell mounts the app routes. The result drives the initial navigation; subsequent navigation is user-driven.

### 4. URL whitelist — internal routes only

Every persisted route (user-pinned, role-default, tenant-default) is validated against a closed prefix whitelist before being written:

| Allowed prefix | Sense |
| -------------- | ----- |
| `/` | Root |
| `/w/{workspace}/...` | Workspace-scoped routes |
| `/{entity}/{id}` | Workspace-agnostic detail (per ADR-044 §5) |
| Any registered standalone route (`/me`, `/help`, `/admin/...`) | Framework-owned shells |

External URLs (`https://...`, `http://...`) are **rejected at write time** with a 400 + clear error message. This eliminates a phishing vector: a compromised admin cannot configure a tenant-default route pointing at `https://evil.example.com` to harvest credentials.

The whitelist is a configurable `LandingRouteOptions.AllowedPrefixes` collection; the framework default ships the standard prefixes above.

### 5. Sticky tier — LocalStorage + cross-device DB sync

The user-sticky tier is updated **on every workspace navigation** by the React shell, with both layers in play:

- **LocalStorage** — instant, per-device. The user sees their last workspace at next login on the same device.
- **Server-side `PUT /api/me/sticky-route`** — eventually-consistent cross-device sync. The user logging in from a new device gets their last-visited from the previous device.

Server-side storage is best-effort — the LocalStorage tier wins on conflict (the user's most recent device-local interaction is more authoritative than a stale server entry).

### 6. Pinned tier — explicit user choice

The user can pin their landing route from a profile UI: `PUT /api/me/landing-route/pinned` with the route as the request body. The route is whitelist-validated. The pinned tier overrides the sticky tier — once pinned, the sticky becomes informational (still updated for the cross-device case, but not consulted unless pinned is cleared).

### 7. Role default tier — Frappe-style admin lever

A tenant admin maps a role to a default route via tenant settings (storage in the tenant's `Granit.Settings` namespace). The route is whitelist-validated. When a user has multiple roles and multiple matching role-defaults, the framework picks **the first one in role-priority order** (defined by `Granit.Authorization`'s role-priority configuration); deterministic, documented.

The role-default route is **the killer feature** of this ADR: a Customer Service role lands on the open-tickets queue, the Accounting role lands on the overdue-invoices list, the Manager role lands on a KPI dashboard. One click saved per session per user.

### 8. Tenant default tier — operator fallback

An operator (Granit Framework workspace admin) configures a single tenant-wide default route. Useful for white-label deployments where the operator wants every user to land on the branded home page, regardless of role. Whitelist-validated.

### 9. Framework default tier — last resort

If all preceding tiers resolve to inaccessible routes (or aren't configured), the framework falls back to `/` (the home page) — which itself routes to the first workspace the user has `Workspace.{Name}.Read` on (alphabetical by `Order`, then by name).

If the user has access to **zero workspaces**, the framework default is `/me/profile` — the user can at least see their own settings and contact an admin.

## Consequences

### Positive

- The 5-tier model captures real-world scenarios cleanly: cross-device sync (sticky), personal preference (pinned), admin-managed defaults per role (role-default), operator-mandated white-label (tenant-default), graceful fallback (framework-default).
- Deep-link routes (Frappe-style) save a click per session per user — measurable productivity gain at scale.
- URL whitelist eliminates the credential-phishing vector for admin-configured routes.
- Graceful fall-through means a removed permission or deleted workspace never produces a login-time 404.

### Negative / accepted trade-offs

- More configuration surface than Notion's last-visited heuristic — but admins typically configure once per role, then forget. The end-user opt-in tiers (sticky, pinned) need zero configuration.
- The role-default tier requires tenant admins to know about the route format. Mitigated by a UI helper that lets the admin pick a route by browsing the workspace tree (rather than typing a URL by hand).
- Multi-role users need a documented role-priority. The framework reuses `Granit.Authorization`'s existing role priority — no new mechanism.

## Cross-references

- [ADR-044](./044-workspace-navigation) — Workspace navigation. The route format (`/w/{workspace}/...`, `/{entity}/{id}`) is owned by ADR-044; this ADR builds on it.
- [ADR-047](./047-entity-view) — `EntityView`. A landing route can include `?view={viewId}` to land on a saved view directly.
- `Granit.Settings`, `Granit.Authorization` — existing modules that store the per-role / per-tenant configuration.

## References

- Frappe role-based default route — adopted as-is for the role-default tier. Most flexible model in the reference space.
- Odoo "Home action" per user — analogous to Granit's user-pinned tier, less general (single window action vs arbitrary route).
- Notion last-visited heuristic — adopted as the sticky tier; combined with explicit configuration tiers for enterprise scenarios.
- ISO 27001 A.9.4.4 (Use of privileged utility programs) — the URL whitelist is what makes an admin-configured landing route auditable: an admin cannot configure a credential-phishing redirect.
