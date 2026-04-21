# Granit.Metering.Endpoints

Minimal API endpoints for Granit.Metering.

## Endpoints

| Method | Route | Permission |
| ------ | ----- | ---------- |
| GET | `/api/granit/metering/meters` | `Metering.Meters.Read` |
| POST | `/api/granit/metering/meters` | `Metering.Meters.Manage` |
| PUT | `/api/granit/metering/meters/{id}` | `Metering.Meters.Manage` |
| GET | `/api/granit/metering/usage` | `Metering.Usage.Read` |
| GET | `/api/granit/metering/quota/{meterId}` | `Metering.Usage.Read` |
| GET | `/api/granit/metering/meter-definitions` (+ `/meta`, `/saved-views/*`) | `Metering.Meters.Read` |
| GET | `/api/granit/metering/usage-aggregates` (+ `/meta`, `/saved-views/*`) | `Metering.Usage.Read` |

The `/meter-definitions` and `/usage-aggregates` routes expose the Granit
QueryEngine (filter, sort, group, paginate, CSV/XLSX export, saved views).
When called without a tenant context (host admin), the multi-tenant query
filter is bypassed for cross-tenant review.
