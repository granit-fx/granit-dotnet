# Granit.Tax.Stripe

Stripe Tax API integration for Granit. Best for B2C global SaaS needing
multi-jurisdiction tax calculation (US Sales Tax, EU VAT, UK VAT).

## Configuration

```json
{
  "Tax": {
    "Stripe": {
      "SecretKey": "<your-stripe-secret-key>",
      "ProductTaxCode": "txcd_10000000"
    }
  }
}
```

> **Security:** Never commit Stripe secret keys. Use environment variables,
> Azure Key Vault, or `Granit.Vault` for production deployments.
