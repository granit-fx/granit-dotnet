# Granit.CustomerBalance.Endpoints

Minimal API endpoints for Granit.CustomerBalance.

## Endpoints

| Method | Route | Permission |
| ------ | ----- | ---------- |
| GET | `/api/granit/customer-balance/balance?currency=EUR` | `CustomerBalance.Accounts.Read` |
| GET | `/api/granit/customer-balance/transactions?currency=EUR` | `CustomerBalance.Transactions.Read` |
| POST | `/api/granit/customer-balance/credit` | `CustomerBalance.Credits.Manage` |
