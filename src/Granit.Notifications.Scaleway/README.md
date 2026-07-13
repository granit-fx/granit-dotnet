# Granit.Notifications.Scaleway

Scaleway Transactional Email provider for `Granit.Notifications.Email`. Registered as Keyed Service with key `"Scaleway"` for multi-provider resolution.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.Scaleway
```

## Configuration

```json
{
  "Notifications": {
    "Email": {
      "Provider": "Scaleway",
      "Scaleway": {
        "SecretKey": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
        "ProjectId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
        "DefaultSenderEmail": "noreply@example.com",
        "DefaultSenderName": "My App",
        "Region": "fr-par"
      }
    }
  }
}
```

Store the secret key in a secret manager (Vault, Azure Key Vault, AWS Secrets Manager).
Never commit it to source control.

## Documentation

See the [full documentation](https://granit-fx.dev).
