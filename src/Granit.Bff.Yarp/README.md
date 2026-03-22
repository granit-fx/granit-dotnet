# Granit.Bff.Yarp

YARP reverse proxy integration for the Granit BFF module.

## Overview

Proxies API requests through the BFF, injecting Bearer tokens from server-side
sessions. CSRF validation is performed on mutating methods (POST/PUT/DELETE/PATCH)
before the request is forwarded to the upstream API.

## Configuration

Routes with `Granit.Bff.RequireAuth = true` metadata receive automatic token
injection. Routes without this metadata are proxied as-is (public).

```json
{
  "ReverseProxy": {
    "Routes": {
      "api-route": {
        "ClusterId": "main-api",
        "Match": { "Path": "/api/{**catch-all}" },
        "Metadata": { "Granit.Bff.RequireAuth": "true" }
      }
    },
    "Clusters": {
      "main-api": {
        "Destinations": {
          "srv1": { "Address": "https://api.example.com:5001" }
        }
      }
    }
  }
}
```

## Usage

```csharp
builder.AddGranitBffYarp();
app.UseGranitBffYarp();
```

## Features

- Bearer token injection from server-side session
- Automatic silent token refresh when expiry approaches
- CSRF validation on mutating HTTP methods
- Multi-service routing via native YARP configuration
