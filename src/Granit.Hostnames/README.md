# Granit.Hostnames

Custom hostname management for Granit. Register a hostname against any tenant resource and resolve an incoming request host to its owner for host-based routing — globally unique hosts (anti-hijacking) with an owner-agnostic model.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Hostnames
```

## What it provides

- **`ManagedHostname`** — an aggregate binding a `Hostname` (lower-case FQDN value object, globally unique) to an opaque owner (`OwnerType` + `OwnerId`, e.g. `"cms.site"`), optionally tenant-scoped, with a canonical (`IsPrimary`) flag and a `HostnameStatus` lifecycle.
- **`IHostnameResolver`** — the read path: resolve a request host to its owner (tenant-agnostic, only active hostnames), for host-based routing in a consumer's middleware.
- **`IManagedHostnameReader` / `IManagedHostnameWriter`** — CQRS read/write contracts.
- Paired query/export definitions for the admin surface.

This is the **layer-pure base package**. Persistence (`IHostnameResolver` / reader / writer impls + the globally unique host index) ships in `Granit.Hostnames.EntityFrameworkCore`; HTTP endpoints in `Granit.Hostnames.Endpoints`; DNS verification and provider (Cloudflare/ACM/…) adapters in their own sibling packages.

> **TLS is an edge concern.** Certificate issuance/renewal, TLS termination and HTTP→HTTPS are handled by the infrastructure edge (Kubernetes ingress + cert-manager, or Cloudflare-for-SaaS / ACM). This capability orchestrates and reflects status — it never performs certificate operations in-process.

## Dependencies

- `Granit`
- `Granit.DataExchange.Abstractions`
- `Granit.QueryEngine.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev).
