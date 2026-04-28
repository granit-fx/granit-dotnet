# Granit.Metering.Endpoints

Minimal API endpoints for Granit.Metering.

## Endpoints

| Method | Route | Permission |
| ------ | ----- | ---------- |
| GET | `/api/{version}/metering/meters` | `Metering.Meters.Read` |
| POST | `/api/{version}/metering/meters` | `Metering.Meters.Manage` |
| PUT | `/api/{version}/metering/meters/{id}` | `Metering.Meters.Manage` |
| GET | `/api/{version}/metering/usage` | `Metering.Usage.Read` |
| GET | `/api/{version}/metering/quota/{meterId}` | `Metering.Usage.Read` |
| GET | `/api/{version}/metering/meter-definitions` (+ `/meta`, `/saved-views/*`) | `Metering.Meters.Read` |
| GET | `/api/{version}/metering/usage-aggregates` (+ `/meta`, `/saved-views/*`) | `Metering.Usage.Read` |

The `/meter-definitions` and `/usage-aggregates` routes expose the Granit
QueryEngine (filter, sort, group, paginate, CSV/XLSX export, saved views).
When called without a tenant context (host admin), the multi-tenant query
filter is bypassed for cross-tenant review.
