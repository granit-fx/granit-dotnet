# Granit.Hostnames.Endpoints

Minimal API endpoints for [`Granit.Hostnames`](../Granit.Hostnames/README.md) custom hostname management.

## Endpoints

| Method | Route | Permission | Description |
|--------|-------|------------|-------------|
| `GET` | `/hostnames` | Read | List hostnames for an owner (`?ownerType=&ownerId=`) |
| `GET` | `/hostnames/availability` | Read | Check if a host is free (`?host=`) |
| `GET` | `/hostnames/{id}` | Read | Get a hostname by id |
| `POST` | `/hostnames` | Manage | Register a new hostname |
| `DELETE` | `/hostnames/{id}` | Delete | Remove a hostname |
| `POST` | `/hostnames/{id}/primary` | Manage | Set as canonical hostname |
| `DELETE` | `/hostnames/{id}/primary` | Manage | Clear canonical flag |

## Registration

```csharp
app.MapGranitHostnames();

// Custom prefix / OpenAPI tag:
app.MapGranitHostnames(opt =>
{
    opt.RoutePrefix = "api/hostnames";
    opt.TagName = "Custom Hostnames";
});
```

## Permissions

| Constant | Value | Side |
|----------|-------|------|
| `HostnamesPermissions.Hostnames.Read` | `Hostnames.Hostnames.Read` | Host |
| `HostnamesPermissions.Hostnames.Manage` | `Hostnames.Hostnames.Manage` | Host |

## Notes

- `POST /hostnames` performs a 409-conflict check before persisting (host already taken).
- `GET /hostnames/availability` is the lightweight pre-flight check for UX validation.
- Primary-flag changes (`POST/DELETE /{id}/primary`) do not automatically cascade to other
  hostnames of the same owner — manage that explicitly.
- The persistence layer is provided by `Granit.Hostnames.EntityFrameworkCore`.
