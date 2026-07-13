# Granit.Notifications.AwsSes

Amazon SES v2 provider for `Granit.Notifications.Email`. Registered as Keyed Service with key `"AwsSes"` for multi-provider resolution.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.AwsSes
```

## Configuration

```json
{
  "Notifications": {
    "Email": {
      "Provider": "AwsSes"
    },
    "AwsSes": {
      "Region": "eu-west-1",
      "FromAddress": "noreply@example.com",
      "ConfigurationSetName": "my-tracking-set"
    }
  }
}
```

When deployed on AWS (ECS, EKS, Lambda), the SDK uses IAM roles automatically.
For local development, set `AccessKeyId` and `SecretAccessKey` or use `aws configure`.

## Documentation

See the [full documentation](https://granit-fx.dev).
