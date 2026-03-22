# Granit.Notifications.Email.SendGrid

SendGrid provider for `Granit.Notifications.Email`. Registered as Keyed Service with key `"SendGrid"` for multi-provider resolution.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.Email.SendGrid
```

## Configuration

```json
{
  "Notifications": {
    "Email": {
      "Provider": "SendGrid",
      "SendGrid": {
        "ApiKey": "SG.xxxxxxxxxxxxxxxxxxxx",
        "DefaultSenderEmail": "noreply@example.com",
        "DefaultSenderName": "My App"
      }
    }
  }
}
```

Store the API key in a secret manager (Vault, Azure Key Vault, AWS Secrets Manager).
Never commit it to source control.

## Documentation

See the [full documentation](https://granit-fx.dev).
