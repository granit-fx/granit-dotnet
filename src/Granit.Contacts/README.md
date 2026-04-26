# Granit.Contacts

Generic party-management module for Granit. The `Contact` aggregate represents any
natural person, legal entity, or organisational unit the platform interacts with —
customers, suppliers, employees, leads, and combinations thereof. Designed dual-use:
host-scoped (`TenantId == null`) for the SaaS host's own contacts, and tenant-scoped
for a tenant's e-commerce / CRM / procurement contacts.

A single contact can hold any combination of `[Flags] ContactRoles` (Customer,
Supplier, Employee, Lead) and supports multi-address (Billing/Shipping/Other),
multi-email and multi-phone (typed: Mobile/Office/Home/Other) with a primary flag
per collection, Person→Company hierarchy via `ParentContactId`, optional user
linkage (`UserId`) for self-service portals, and polyglot external mappings keyed
by provider name (one mapping per provider).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Contacts
```

## Dependencies

- `Granit`
- `Granit.Guids`
- `Granit.Timing`

## Documentation

See the [full documentation](https://granit-fx.dev).
