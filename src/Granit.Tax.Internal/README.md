# Granit.Tax.Internal

Self-hosted EU VAT tax calculator for the Granit framework.

## Features

- **EU VAT rule engine** — 5 scenarios: domestic, reverse charge, OSS, B2C, export
- **VIES online validation** — EU Commission REST API with Polly resilience
- **Offline fallback** — accepts reverse charge on format validation when VIES is down
- **27 EU standard rates** — built-in defaults, configurable overrides
- **Northern Ireland** — excluded for digital services (post-Brexit)

## Configuration

```json
{
  "Tax": {
    "SellerCountryCode": "BE",
    "SellerVatNumber": "BE0123456789",
    "OssEnabled": true,
    "AllowOfflineFallback": true
  }
}
```
