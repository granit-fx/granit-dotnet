# Granit.Notifications.AI

AI-powered notification content generation and smart channel routing for Granit.Notifications. Uses LLM-based analysis to generate localized notification subject/body and recommend optimal delivery channels based on context (urgency, time of day, user preferences).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.AI
```

## Personal data and the LLM (GDPR)

By default the notification `Data` payload is **not** forwarded to the model — only the
notification type, severity and culture are. The payload may contain personal data, so
forwarding it is an explicit opt-in:

```json
{
  "Notifications": {
    "AI": {
      "AllowPersonalDataInPrompts": true
    }
  }
}
```

Before enabling, verify your AI provider agreement covers personal-data processing
(GDPR Art. 28 processor terms) and document the flow in your record of processing
activities.

## Dependencies

- `Granit.AI`
- `Granit.Notifications`

## Documentation

See the [full documentation](https://granit-fx.dev).
