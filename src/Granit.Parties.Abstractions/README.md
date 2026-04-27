# Granit.Parties.Abstractions

Lightweight abstractions for [Granit.Parties](../Granit.Parties/README.md). Reference
this package — instead of the full `Granit.Parties` — when your module only needs to
carry a `PartyId` on its own aggregates, publish or consume contact-related integration
events, or accept a `BillingAddress` as input (e.g., a tax calculator). It pulls in the
value objects and enums without inheriting the full `Party` aggregate, EF Core, or
persistence stack.

What's inside:

- Value objects: `PartyId`, `Address`, `BillingAddress`
- Enums: `PartyKind`, `PartyRoles` (Flags), `PartyStatus`, `AddressKind`, `PhoneKind`
- Reserved external-provider names (`PartyExternalProviderNames`)
- Integration events (`Party*Eto`)

Modules that own the contact aggregate and its readers / writers / resolvers / seeders
should reference `Granit.Parties` directly.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Parties.Abstractions
```

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
