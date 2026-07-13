# Granit.Notifications.AwsSns

AWS SNS provider for Granit.Notifications — one package for both SNS-backed capabilities:

- **SMS** (`ISmsSender`, keyed `"AwsSns"`): direct SMS publish via SNS.
- **Mobile push** (`IMobilePushSender`, keyed `"AwsSns"`): APNs/FCM delivery through SNS
  platform endpoints.

Both share the same `AWSSDK.SimpleNotificationService` client and AWS credentials.
Registering the module wires both senders; an unused capability is inert — keyed senders
are only resolved when a channel's `Provider` option selects them.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.AwsSns
```

## Configuration

```json
{
  "Notifications": {
    "AwsSns": {
      "Sms": { "Region": "eu-west-1" },
      "MobilePush": { "Region": "eu-west-1", "PlatformApplicationArn": "arn:aws:sns:..." }
    },
    "Sms": { "Provider": "AwsSns" },
    "MobilePush": { "Provider": "AwsSns" }
  }
}
```

## Dependencies

- `Granit.Notifications.MobilePush`
- `Granit.Notifications.Sms`
- `AWSSDK.SimpleNotificationService`

## Documentation

See the [full documentation](https://granit-fx.dev).
