# Granit.Payments.SepaDirectDebit.Internal

Self-hosted SEPA Direct Debit provider. Zero external dependency.

## How it works

1. **Mandate**: created offline (paper/email), activated manually
2. **Collection**: batched into PAIN.008 XML, sent to bank
3. **Reconciliation**: CAMT.053 import updates collection status

## PAIN.008

- ISO 20022 PAIN.008.001.08 format
- Sequence type: always `RCUR` (post-2016 rulebook, no FRST/FNAL)
- Groups by scheme (CORE/B2B)

## Configuration

```json
{
  "Payments": {
    "SepaDirectDebit": {
      "Internal": {
        "CreditorIban": "BE68 5390 0754 7034",
        "CreditorBic": "TRIOBEBB",
        "CreditorName": "My Company SA",
        "CreditorId": "BE68ZZZ0123456789"
      }
    }
  }
}
```
