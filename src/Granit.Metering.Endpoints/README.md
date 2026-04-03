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
