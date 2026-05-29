# Granit.Identity.Notifications

Recipient resolution bridge between `Granit.Notifications` and `Granit.Identity`.
Provides a default `IRecipientResolver` that turns a user ID into `RecipientInfo`
(email, phone number, preferred culture, display name) by reading from the
Identity module's `IIdentityUserReader`.

A single adapter covers **local (OpenIddict)** and **every federated provider**
(Keycloak, EntraID, Cognito, Google), because `IIdentityUserReader` is the
unified read abstraction across providers. In federated mode it reads through the
user cache, so resolution avoids a round-trip to the external identity provider.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Identity.Notifications
```

## Usage

Load `GranitIdentityNotificationsModule` (it depends on `GranitIdentityModule`
and `GranitNotificationsAbstractionsModule`), or register manually:

```csharp
services.AddGranitIdentityRecipientResolver();
```

The resolver is registered with `TryAddScoped`, so an application that supplies
its own `IRecipientResolver` keeps full control.

## How fields are resolved

`Email`, `FirstName`/`LastName` (→ `DisplayName`) come straight from
`IIdentityUser`. The canonical `User` aggregate (ADR-051) additionally exposes
`PhoneNumber` and `PreferredLocale` as first-class fields, which take precedence.
For backends that surface contact data only through the provider metadata bag
(federated cache entries, custom claims), the resolver falls back to configurable
metadata keys.

## Configuration

```json
{
  "Identity": {
    "RecipientResolver": {
      "PhoneNumberMetadataKeys": ["phoneNumber", "phone_number"],
      "PreferredCultureMetadataKeys": ["locale", "preferredLanguage"],
      "DefaultCulture": null
    }
  }
}
```

| Setting | Default | Purpose |
| --- | --- | --- |
| `PhoneNumberMetadataKeys` | `["phoneNumber", "phone_number"]` | Metadata keys probed in order for the phone number (Keycloak / Cognito) |
| `PreferredCultureMetadataKeys` | `["locale", "preferredLanguage"]` | Metadata keys probed in order for the BCP-47 culture (Keycloak / Cognito / Graph) |
| `DefaultCulture` | `null` | Culture applied when none is resolved; `null` leaves it to the notification pipeline |

## Dependencies

- `Granit.Identity`
- `Granit.Notifications.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev).
