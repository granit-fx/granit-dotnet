# Granit.CustomerBalance.Endpoints

Minimal API endpoints for Granit.CustomerBalance.

## Endpoints

| Method | Route | Permission |
| ------ | ----- | ---------- |
| GET | `/api/{version}/customer-balance/balance?currency=EUR` | `CustomerBalance.Accounts.Read` |
| GET | `/api/{version}/customer-balance/transactions?currency=EUR` | `CustomerBalance.Transactions.Read` |
| POST | `/api/{version}/customer-balance/credit` | `CustomerBalance.Credits.Manage` |
