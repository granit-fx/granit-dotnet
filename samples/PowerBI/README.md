# Power BI Desktop samples

Two `.pbids` connector descriptors for Power BI Desktop, one per OData
mount that `Granit.Http.ODataExposure` can expose:

| File | Mount | Audience |
| ---- | ----- | -------- |
| `granit-showcase.pbids` | `/api/v1/odata` (tenant-feed) | Tenant analysts — sees only their tenant's rows. |
| `granit-showcase-host.pbids` | `/api/v1/odata/host` (host-feed) | Host operators (finance ops, compliance, capacity planning) — sees data **across** tenants. Gated by `MultiTenancySides.Host` permissions. |

Open either file from Power BI Desktop → *File → Open report*. Power BI
prompts for authentication, then lands the analyst in the Navigator
listing the EntitySets they have permission to read.

## Customise the URL

Replace `https://your-app.example.com` with your deployment's actual
host. The path follows the framework's API versioning convention
`/api/{version}/odata{/host}` — `{version}` resolves to `v1` / `v2` /
… per the host's `Granit.Http.ApiVersioning` configuration. These
samples pin `v1`; bump to match your deployment.

## Authentication

The Granit OData layer uses the host's existing OAuth2 / DPoP stack
from `Granit.Identity`. Power BI Desktop signs the analyst in via the
"Organizational account" auth method on first connection.

For the host-feed, the analyst must be authenticated against an
identity that carries an `OData.Host.{Module}.{Entity}.Read` permission
declared as `MultiTenancySides.Host`. A tenant user's bearer token will
not carry these permissions; the Navigator will return zero EntitySets
(every set is `403`). For unattended Power BI Service refresh jobs, use
a dedicated service principal scoped to the host realm.

## Tenant-feed vs host-feed — pick the right one

- **Tenant-feed** (`granit-showcase.pbids`) — your default. Every BI
  use case scoped to a single tenant's data goes here.
- **Host-feed** (`granit-showcase-host.pbids`) — only when the use case
  is legitimately cross-tenant: MRR/ARR rollups, compliance audit
  trails across tenants, capacity-planning aggregates, churn analysis
  by plan. Mounting it is an explicit opt-in on the application side
  (`MapGranitODataHostEndpoints`); using the wrong file with the wrong
  identity will surface a `403` immediately.

## Full guides

- [Tenant-feed walkthrough](https://granit-fx.github.io/granit-dotnet/dotnet/business/analytics/odata-power-bi/) —
  end-to-end auth setup, refresh schedules, rate-limit awareness, known
  limits, example DAX measures, troubleshooting.
- [Host-feed walkthrough](https://granit-fx.github.io/granit-dotnet/dotnet/business/analytics/odata-host-feed/) —
  cross-tenant BI, three strict-config gates, `MultiTenancySides.Host`
  permission convention, decision matrix vs `Granit.Analytics`.
