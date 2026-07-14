# Granit.Http.Cookies.EntityFrameworkCore

EF Core persistence for the `Granit.Http.Cookies` consent ledger. Provides the isolated
`CookiesDbContext`, the append-only `EfCoreConsentLedger` (GDPR Art. 7(1) accountability,
`ConsentRecordedEto` dispatched after each durable write), a GDPR Art. 17 erasure
primitive (`ICookieConsentEraser`), and the query-engine source for consent statistics.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.Cookies.EntityFrameworkCore
```

## Dependencies

- `Granit.Http.Cookies`
- `Granit.Persistence.EntityFrameworkCore`

## Getting started

Register the isolated `CookiesDbContext` — this replaces the base module's no-op
`NullConsentLedger` with the durable EF Core ledger:

```csharp
builder.AddGranitCookiesEntityFrameworkCore(options => options.UseNpgsql(connectionString));
```

Every `POST /cookies/consent` decision (see `Granit.Http.Cookies.Endpoints`) is then
appended to the `cookie_consent_records` table: granted/denied categories, consent mode,
CMP source, irreversibly anonymized IP, truncated user-agent, and correlation id —
never a raw identifier.

Tables default to the host schema via `GranitHttpCookiesDbProperties.DbSchema` — set this
static property **before** `ConfigureServices` completes (EF Core caches the compiled
model). The framework ships no migrations: the application owns them. A host context can
map the table itself via `modelBuilder.ConfigureCookiesModule(excludeFromMigrations: true)`
(exactly one context may own the DDL).

## GDPR erasure

Consent records are anonymous by design; the only subject link is the `CreatedBy` audit
field stamped for authenticated posters. `ICookieConsentEraser.EraseUserDataAsync` hard
deletes those rows — wire it into your personal-data deletion pipeline (see
`Granit.Privacy`). Anonymous rows are retained: they identify nobody.

## Documentation

See the [full documentation](https://granit-fx.dev).
