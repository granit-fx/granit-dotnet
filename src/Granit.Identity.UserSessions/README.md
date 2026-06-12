# Granit.Identity.UserSessions

Makes the **identity provider** the backend for `granit`'s canonical user-session API.
Install it alongside `Granit.UserSessions.Endpoints` and a user's SSO sessions and
devices surface — and revoke — through `/sessions` and `/devices`, with no IdP-specific
client code. Works for the no-BFF topology.

Part of the [granit](https://granit-fx.dev) framework.

## What it does

Registers `IdentityUserSessionProvider` and `IdentityUserDeviceProvider` as the active
providers (replacing the no-op defaults), implemented over `IIdentitySessionManager`:

- **`/sessions`** — the IdP's active SSO sessions (id, started/last-access, IP → location
  resolved by the manager); revoke one (the caller's own) or all but the current one.
- **`/devices`** — the IdP's device-activity view (OS, browser, last seen, session count).

Provider-agnostic: it depends only on `IIdentitySessionManager`, so any IdP integration
that implements it is covered — **OpenIddict** today; **Keycloak** as soon as it provides
an `IIdentitySessionManager`.

## Notes

- Devices are classified `DeviceKind.Browser` (IdP SSO is browser-based); a precise kind
  needs the OIDC client to declare it.
- It carries no policy — authorization, audit and geo/risk enrichment stay in
  `IUserSessionManager`.
