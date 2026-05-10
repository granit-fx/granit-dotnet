# Issue1822 WebApp Sample

This is a small ASP.NET Core web app demonstrating the bulk action executor flow for invoices.

Endpoints:
- `GET /api/invoices/seed` — seed sample invoices
- `GET /api/invoices` — list invoices
- `POST /api/entities/invoices/bulk/archive` — perform bulk archive; JSON body: `{ "ids": ["guid", ...], "payload": {} }`

Run:
```powershell
cd samples/Issue1822.WebApp
dotnet run
# app listens on http://localhost:5005
```
