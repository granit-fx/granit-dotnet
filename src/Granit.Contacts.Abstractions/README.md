# Granit.Contacts.Abstractions

Lightweight abstractions for [Granit.Contacts](../Granit.Contacts/README.md). Reference
this package — instead of the full `Granit.Contacts` — when your module only needs to
carry a `ContactId` on its own aggregates, publish or consume contact-related integration
events, or accept a `BillingAddress` as input (e.g., a tax calculator). It pulls in the
value objects and enums without inheriting the full `Contact` aggregate, EF Core, or
persistence stack.

What's inside:

- Value objects: `ContactId`, `Address`, `BillingAddress`
- Enums: `ContactKind`, `ContactRoles` (Flags), `ContactStatus`, `AddressKind`, `PhoneKind`
- Reserved external-provider names (`ContactExternalProviderNames`)
- Integration events (`Contact*Eto`)

Modules that own the contact aggregate and its readers / writers / resolvers / seeders
should reference `Granit.Contacts` directly.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Contacts.Abstractions
```

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
