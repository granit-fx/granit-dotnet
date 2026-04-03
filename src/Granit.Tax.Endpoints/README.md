# Granit.Tax.Endpoints

Admin API endpoints for Granit.Tax.

## Endpoints

| Method | Route | Permission |
| ------ | ----- | ---------- |
| POST | `/api/granit/tax/validate` | `Tax.Validations.Execute` |
| GET | `/api/granit/tax/rates` | `Tax.Rates.Read` |
| GET | `/api/granit/tax/rates/{countryCode}` | `Tax.Rates.Read` |
| POST | `/api/granit/tax/calculate` | `Tax.Validations.Execute` |
