# Granit.Payments.SepaDirectDebit.GoCardless

GoCardless provider for SEPA Direct Debit. Managed mandate lifecycle
and automated collection via GoCardless API.

## Configuration

```json
{
  "Payments": {
    "SepaDirectDebit": {
      "GoCardless": {
        "AccessToken": "sandbox_xxx",
        "WebhookSecret": "webhook_secret_xxx",
        "UseSandbox": true
      }
    }
  }
}
```
