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

## Two usage modes

The aggregate is `IMultiTenant`, but unlike most Granit modules it intentionally treats
`TenantId == null` as a **first-class scope** — the SaaS host's own contacts — rather
than as "missing data". The store's read path enforces the corresponding visibility
rules without consumer code branches:

| Active context | What the store returns |
| -------------- | ---------------------- |
| `ICurrentTenant.IsAvailable == true` (tenant `T`) | Only contacts where `TenantId == T` |
| `ICurrentTenant.IsAvailable == false` (host) | Only contacts where `TenantId == null` |
| Any context, after `IDataFilter.Disable<IMultiTenant>()` | All contacts cross-scope |

Two example call sites:

```csharp
// Host SaaS — billing relationships with the platform's own tenants-as-customers.
Contact platformCustomer = Contact.Create(
    Guid.NewGuid(), tenantId: null, ContactKind.Company, "ACME Inc.", "EUR");

// Tenant e-commerce app — that tenant's own end-customer base.
Contact endCustomer = Contact.Create(
    Guid.NewGuid(), tenantId: currentTenant.Id, ContactKind.Individual, "Jean Dupont", "EUR");
```

Cross-scope reads (e.g., a host admin browsing a tenant's contacts during a support
incident) require an explicit `using IDataFilter.Disable<IMultiTenant>()` scope and
should be reviewed for privacy implications.

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
