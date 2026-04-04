# Granit.Payments.SepaTransfer

Self-hosted SEPA bank transfer payment provider. Zero external dependency —
the ultimate sovereignty option.

## How it works

1. **Checkout**: returns bank transfer instructions (IBAN, BIC, structured reference, amount)
2. **Customer**: initiates bank transfer from their bank
3. **Reconciliation**: operator uploads bank statement (CAMT.053) → automatic matching
4. **Match**: structured reference links transfer to invoice → transaction marked Succeeded

## Configuration

```json
{
  "Payments": {
    "SepaTransfer": {
      "Iban": "BE68 5390 0754 7034",
      "Bic": "TRIOBEBB",
      "BeneficiaryName": "My Company SA",
      "ReferencePrefix": "GRN",
      "ExpirationDays": 14
    }
  }
}
```

## Supported formats

| Format | Parser | Status |
| ------ | ------ | ------ |
| CAMT.053 (ISO 20022) | `Camt053Parser` | Implemented |
| MT940 (SWIFT) | — | Phase 3 |
| CSV (bank-specific) | — | Phase 3 |
