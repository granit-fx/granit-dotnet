# Granit.Notifications.Sms.AzureCommunicationServices

Azure Communication Services SMS provider for `Granit.Notifications.Sms`. Registered as Keyed Service with key `"AzureCommunicationServices"` for multi-provider resolution.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.Sms.AzureCommunicationServices
```

## Configuration

```json
{
  "Notifications": {
    "Sms": {
      "Provider": "AzureCommunicationServices"
    }
  },
  "AzureCommunicationServices": {
    "Sms": {
      "ConnectionString": "endpoint=https://my-acs.communication.azure.com/;accesskey=...",
      "FromPhoneNumber": "+15551234567"
    }
  }
}
```

When deployed on Azure with managed identity, use `Endpoint` instead of `ConnectionString`:

```json
{
  "AzureCommunicationServices": {
    "Sms": {
      "Endpoint": "https://my-acs.communication.azure.com",
      "FromPhoneNumber": "+15551234567"
    }
  }
}
```

## Documentation

See the [full documentation](https://granit-fx.dev).
