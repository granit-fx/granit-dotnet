# Granit.Tax.Endpoints

Admin API endpoints for Granit.Tax.

## Endpoints

| Method | Route | Permission |
| ------ | ----- | ---------- |
| POST | `/api/{version}/tax/validate` | `Tax.Validations.Execute` |
| GET | `/api/{version}/tax/rates` | `Tax.Rates.Read` |
| GET | `/api/{version}/tax/rates/{countryCode}` | `Tax.Rates.Read` |
| POST | `/api/{version}/tax/calculate` | `Tax.Validations.Execute` |
