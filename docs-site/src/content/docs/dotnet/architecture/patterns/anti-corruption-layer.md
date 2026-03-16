---
title: "Anti-Corruption Layer"
description: "Translation layer isolating domain models from external API models and SDKs"
sidebar:
  order: 8
---

## Definition

The Anti-Corruption Layer (ACL) isolates the internal domain model from
external models (third-party APIs, SDKs, legacy formats). A translation layer
converts external DTOs into Granit domain types, preventing foreign concepts
from "polluting" the framework core.

## Diagram

```mermaid
flowchart LR
    subgraph External["External services"]
        KC["Keycloak Admin API"]
        S3["AWS S3 SDK"]
        BR["Brevo API"]
        FCM["Firebase FCM"]
        MK["MailKit / SMTP"]
    end

    subgraph ACL["Anti-Corruption Layer"]
        KC_DTO["KeycloakUserRepresentation"]
        KC_MAP["ToIdentityUser()"]
        S3_ADP["S3BlobClient"]
        BR_ADP["BrevoNotificationProvider"]
        FCM_ADP["GoogleFcmMobilePushSender"]
        MK_ADP["MailKitEmailSender"]
    end

    subgraph Domain["Granit domain model"]
        IU["IdentityUser"]
        BD["BlobDescriptor"]
        EM["EmailMessage"]
        PM["MobilePushMessage"]
    end

    KC --> KC_DTO --> KC_MAP --> IU
    S3 --> S3_ADP --> BD
    BR --> BR_ADP --> EM
    FCM --> FCM_ADP --> PM
    MK --> MK_ADP --> EM

    style External fill:#fef0f0,stroke:#c44e4e
    style ACL fill:#fef3e0,stroke:#e8a317
    style Domain fill:#e8fde8,stroke:#2d8a4e
```

## Implementation in Granit

### Keycloak -- the most comprehensive case

`Granit.Identity.Keycloak` is the framework's canonical ACL. Keycloak responses
are deserialized into `internal` DTOs (`KeycloakUserRepresentation`,
`KeycloakSessionRepresentation`, etc.) then converted to domain models via
`private static` methods:

```csharp
// External DTO (internal, never exposed)
internal sealed record KeycloakUserRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("email")] string? Email,
    // ...
);

// Conversion to domain model
private static IdentityUser ToIdentityUser(KeycloakUserRepresentation user) =>
    new(user.Id, user.Username, user.Email, user.FirstName, user.LastName,
        user.Enabled, FlattenAttributes(user.Attributes));
```

**Specifics**:

- `FlattenAttributes()` -- converts Keycloak `Dictionary(string, List(string))`
  to Granit `Dictionary(string, string)` (multi-value attributes to single value)
- `ToIdentitySession()` -- converts Unix timestamps (milliseconds) to `DateTimeOffset`
- `ToIdentityGroup()` -- recursive mapping for subgroups

### Claims transformation (JWT)

`KeycloakClaimsTransformation` and `EntraIdClaimsTransformation` extract roles
from proprietary JSON structures (`realm_access.roles`,
`resource_access.{client}.roles`) and convert them to standard .NET
`ClaimTypes.Role` claims.

### ACL inventory in Granit

| External service | ACL package | External DTO to internal model |
| --- | --- | --- |
| Keycloak Admin API | `Granit.Identity.Keycloak` | `KeycloakUserRepresentation` to `IdentityUser` |
| Keycloak JWT | `Granit.Authentication.Keycloak` | JSON `realm_access` to `ClaimTypes.Role` |
| Entra ID JWT | `Granit.Authentication.EntraId` | JSON `roles` (v1.0/v2.0) to `ClaimTypes.Role` |
| AWS S3 SDK | `Granit.BlobStorage.S3` | `GetPreSignedUrlRequest` from `BlobUploadRequest` |
| MailKit SMTP | `Granit.Notifications.Email.Smtp` | `MimeMessage` from `EmailMessage` |
| Brevo API | `Granit.Notifications.Brevo` | JSON payload from `EmailMessage` / `SmsMessage` / `WhatsAppMessage` |
| Firebase FCM | `Granit.Notifications.MobilePush.GoogleFcm` | `FcmPayload` from `MobilePushMessage` |
| ImageMagick | `Granit.Imaging.MagickNet` | `MagickFormat` to/from `ImageFormat` (bidirectional) |
| Import systems | `Granit.DataExchange.EntityFrameworkCore` | External ID (Odoo `__export__`) to internal Entity ID |

### Architectural principles

1. **External DTOs are `internal`** -- never exposed outside the adapter package
2. **Conversion methods are `private static`** -- isolated from the rest of the code
3. **Error transformation** -- external errors are parsed and encapsulated in domain exceptions
4. **Graceful degradation** -- reads log a warning and return `null`; writes propagate the exception
5. **`[ExcludeFromCodeCoverage]`** -- on adapters requiring a live service (S3, SMTP)

### Reference files

| File | Role |
| --- | --- |
| `src/Granit.Identity.Keycloak/Internal/KeycloakIdentityProvider.cs` | Primary ACL (9 conversion methods) |
| `src/Granit.Identity.Keycloak/Internal/KeycloakUserRepresentation.cs` | Keycloak external DTO |
| `src/Granit.Authentication.Keycloak/Authentication/KeycloakClaimsTransformation.cs` | JWT claims to Role |
| `src/Granit.BlobStorage.S3/Internal/S3BlobClient.cs` | S3 adapter |
| `src/Granit.Notifications.Email.Smtp/Internal/MailKitEmailSender.cs` | SMTP adapter |
| `src/Granit.Notifications.Brevo/Internal/BrevoNotificationProvider.cs` | Multi-channel Brevo |
| `src/Granit.Notifications.MobilePush.GoogleFcm/Internal/GoogleFcmMobilePushSender.cs` | FCM adapter |
| `src/Granit.Imaging.MagickNet/Internal/MagickFormatMapper.cs` | Image format mapping |

## Rationale

| Problem | ACL solution |
| --- | --- |
| Keycloak models in the domain = tight coupling | `internal` DTOs + static conversion |
| Keycloak API change = impact across the entire codebase | Only the adapter changes, the domain remains stable |
| JWT claims format differs between Keycloak and Entra ID | Dedicated transformations, unified `ClaimTypes.Role` model |
| Multi-value Keycloak attributes vs single-value Granit | `FlattenAttributes()` in the ACL |
| Unix timestamps (ms) in Keycloak vs `DateTimeOffset` in .NET | Conversion in `ToIdentitySession()` |

## Usage example

```csharp
// The endpoint only knows the IdentityUser domain model,
// never the Keycloak DTOs

private static async Task<Results<Ok<IdentityUser>, NotFound>> GetUserAsync(
    string userId,
    IIdentityProvider identityProvider,
    CancellationToken cancellationToken)
{
    // IIdentityProvider is implemented by KeycloakIdentityProvider
    // which does: API call > KeycloakUserRepresentation > ToIdentityUser()
    IdentityUser? user = await identityProvider
        .FindByIdAsync(userId, cancellationToken)
        .ConfigureAwait(false);

    return user is not null
        ? TypedResults.Ok(user)
        : TypedResults.NotFound();
}
```

## Further reading

- [Anti-Corruption Layer -- Microsoft Cloud Design Patterns](https://learn.microsoft.com/en-us/azure/architecture/patterns/anti-corruption-layer)
