# Granit.Validation.Finance

Banking and payment identifier validators for Granit, as FluentValidation extensions and server validators:

- **Account / creditor identifiers**: IBAN (ISO 13616), BIC/SWIFT (ISO 9362), SEPA Creditor Identifier (EPC262).
- **Domestic routing / clearing codes**: US ABA routing, Australian/NZ BSB, Canadian routing, Indian IFSC.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Validation.Finance
```

## Usage

```csharp
RuleFor(x => x.Iban).Iban();
RuleFor(x => x.Routing).AbaRouting();
```

## Documentation

See the [full documentation](https://granit-fx.dev).
