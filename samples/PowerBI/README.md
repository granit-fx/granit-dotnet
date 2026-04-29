# Power BI Desktop sample

`granit-showcase.pbids` is a Power BI Desktop connector descriptor that
points at a Granit-based application's OData v4 feed. Open it from
Power BI Desktop → *File → Open report* and Power BI prompts for
authentication, then lands the analyst in the Navigator.

## Customise the URL

Replace `https://your-app.example.com/api/v1/odata` with your
deployment's actual OData root (the prefix you passed to
`MapGranitODataEndpoints`). The path follows the framework's API
versioning convention `/api/{version}/odata` — `{version}` resolves
to `v1` / `v2` / … per the host's `Granit.Http.ApiVersioning`
configuration. This sample pins `v1`; bump to match your deployment.

## Authentication

The Granit OData layer uses the host's existing OAuth2 / DPoP stack
from `Granit.Identity`. Power BI Desktop signs the analyst in via the
"Organizational account" auth method on first connection.

## Full guide

See [`dotnet/business/analytics/odata-power-bi`](https://granit-fx.github.io/granit-dotnet/dotnet/business/analytics/odata-power-bi/)
in the docs-site for the end-to-end walkthrough — auth setup, refresh
schedules, rate-limit awareness, known limits, example DAX measures,
and troubleshooting.
