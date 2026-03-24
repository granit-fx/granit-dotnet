# Granit.Authentication.Oidc

Strongly typed OIDC/OAuth 2.0 protocol primitives for the Granit framework.

## Install

```sh
dotnet add package Granit.Authentication.Oidc
```

## Features

- `OidcConstants` -- all standard OAuth/OIDC parameter names and values
- Discovery document fetching and caching (`IDiscoveryDocumentService`)
- Typed token request/response models
- Client authentication strategies (`client_secret_post`, `private_key_jwt`)
- DPoP proof generation (RFC 9449)
- PKCE helpers (RFC 7636)
