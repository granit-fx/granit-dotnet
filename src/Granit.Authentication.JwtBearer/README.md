# Granit.Authentication.JwtBearer

Generic JWT Bearer authentication (OIDC) for Granit applications. Provides `ICurrentUserService`,
`JwtBearerAuthOptions`, the `Authenticated` policy, and provider-agnostic OIDC Back-Channel Logout
support (`IRevokedSessionStore`, `BackChannelLogoutTokenValidator`, `MapBackChannelLogout`).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Authentication.JwtBearer
```

## Dependencies

- `Granit.Users`

## Documentation

See the [full documentation](https://granit-fx.dev).
