# Granit.Notifications.AzureCommunicationServices

Azure Communication Services provider for Granit.Notifications — one package for both
ACS-backed capabilities:

- **Email** (`IEmailSender`, keyed `"AzureCommunicationServices"`).
- **SMS** (`ISmsSender`, keyed `"AzureCommunicationServices"`).

Both share the same ACS resource (connection string or endpoint + managed identity).
Registering the module wires both senders; an unused capability is inert — keyed senders
are only resolved when a channel's `Provider` option selects them.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.AzureCommunicationServices
```

## Configuration

```json
{
  "Notifications": {
    "AzureCommunicationServices": {
      "Email": { "ConnectionString": "<from Vault>", "DefaultSenderEmail": "no-reply@example.com" },
      "Sms": { "ConnectionString": "<from Vault>", "SenderPhoneNumber": "+3225551234" }
    },
    "Email": { "Provider": "AzureCommunicationServices" },
    "Sms": { "Provider": "AzureCommunicationServices" }
  }
}
```

## Dependencies

- `Granit.Notifications.Email`
- `Granit.Notifications.Sms`
- `Azure.Communication.Email` / `Azure.Communication.Sms` / `Azure.Identity`

## Documentation

See the [full documentation](https://granit-fx.dev).
