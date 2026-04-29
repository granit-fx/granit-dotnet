# Granit.Analytics.PostGIS

Opt-in geography provider for the `MapWidget` shipped by `Granit.Analytics`.
Once registered, hosts can configure a `MapWidgetDefinition` with a single
PostGIS `geography(Point)` column instead of two surface `Latitude` / `Longitude`
decimal columns — the existing schema is reused for dashboards without
duplicating coordinates.

## Why

`Granit.Analytics` ships the `MapPointSource.LatLng` path on any database:
two decimal columns, no special library, no provider lock-in. The
`MapPointSource.Geography` path is opt-in because it pulls
`NetTopologySuite` (BSD-3-Clause) — hosts that don't run on PostgreSQL +
PostGIS pay zero overhead.

Useful any time the host already stores location data in PostGIS:

- Sales territories, customer addresses, branch offices.
- Logistics: deliveries, route polylines.
- Field operations: on-site interventions, asset locations.

Real-time / live tracking (WebSocket-pushed device positions) lives in a
separate epic — this package only handles the snapshot read path.

## Usage

```csharp
builder.Services.AddGranitAnalyticsPostGIS();
// + Npgsql with NetTopologySuite plugin on the consuming DbContext:
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString, o => o.UseNetTopologySuite()));
```

Then a `MapWidgetDefinition` configured with
`new MapPointSource.Geography(GeographyColumn: "Location")` will project the
`Location` `Point` column on the entity into the standard
`{ lat, lng, id, popup }` snapshot — frontend code is unchanged.

## What it ships

- `IGeographyPointProjector<TEntity>` is the abstraction declared in
  `Granit.Analytics`. This package provides the open-generic
  `NtsGeographyPointProjector<TEntity>` implementation registered scoped via
  `AddGranitAnalyticsPostGIS()`.
- WGS84 axis-order convention (`Point.X` = longitude, `Point.Y` = latitude)
  applied at the projection boundary.
- Null `Point` rows are silently dropped (mirrors null lat/lng behaviour);
  out-of-range coordinates are dropped and counted on the existing
  `granit.analytics.map.invalid_coordinates` OTel counter.

## Limits

- Only `geography(Point)` is supported in v1. Polygons, line strings, and
  spatial query operators (`ST_DWithin`, `ST_Within`) are out of scope —
  follow-up stories.
- Single-column projection per widget instance. A widget configured with two
  coordinate columns must use `MapPointSource.LatLng`.
