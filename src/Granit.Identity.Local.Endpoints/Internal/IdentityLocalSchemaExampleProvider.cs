using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.Identity.Local.Endpoints.Dtos;

namespace Granit.Identity.Local.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for Identity.Local Request DTOs (login, registration,
/// 2FA, passkeys, password and profile management).
/// </summary>
internal sealed class IdentityLocalSchemaExampleProvider : ISchemaExampleProvider
{
    private const string PasswordField = "password";
    private const string PasswordPlaceholder = "<your-password>";

    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            // ──── Login / registration ────
            // Placeholder credentials use angle-bracket syntax (<…>) so secret scanners
            // and the GRSEC003 analyzer recognize them as example markers, not literals.
            [typeof(AccountLoginRequest)] = new JsonObject
            {
                ["login"] = "alice@acme.example",
                [PasswordField] = PasswordPlaceholder,
                ["rememberMe"] = true,
            },
            [typeof(AccountLoginResponse)] = new JsonObject
            {
                ["succeeded"] = false,
                ["requiresTwoFactor"] = true,
                ["isLockedOut"] = false,
                ["isNotAllowed"] = false,
            },
            [typeof(AccountRegisterRequest)] = new JsonObject
            {
                ["email"] = "alice@acme.example",
                [PasswordField] = PasswordPlaceholder,
                ["firstName"] = "Alice",
                ["lastName"] = "Martin",
            },

            // ──── Two-factor authentication ────
            [typeof(AccountTwoFactorLoginRequest)] = new JsonObject
            {
                ["code"] = "123456",
                ["useRecoveryCode"] = false,
                ["rememberMe"] = false,
            },
            [typeof(AccountTwoFactorEnableRequest)] = new JsonObject
            {
                ["code"] = "123456",
            },
            [typeof(AccountTwoFactorDisableRequest)] = new JsonObject
            {
                [PasswordField] = PasswordPlaceholder,
            },

            // ──── Password management ────
            [typeof(AccountPasswordChangeRequest)] = new JsonObject
            {
                ["currentPassword"] = "<current-password>",
                ["newPassword"] = "<new-password>",
            },
            [typeof(AccountForgotPasswordRequest)] = new JsonObject
            {
                ["email"] = "alice@acme.example",
            },
            [typeof(AccountPasswordResetRequest)] = new JsonObject
            {
                ["userId"] = "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                ["token"] = "<reset-token-from-email>",
                ["newPassword"] = "<new-password>",
            },

            // ──── Email change ────
            [typeof(AccountChangeEmailRequest)] = new JsonObject
            {
                ["newEmail"] = "alice.new@acme.example",
            },
            [typeof(AccountConfirmEmailChangeRequest)] = new JsonObject
            {
                ["userId"] = "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                ["newEmail"] = "alice.new@acme.example",
                ["token"] = "<confirmation-token-from-email>",
            },

            // ──── Profile ────
            [typeof(AccountProfileUpdateRequest)] = new JsonObject
            {
                ["firstName"] = "Alice",
                ["lastName"] = "Martin",
            },
            [typeof(AccountDeleteRequest)] = new JsonObject
            {
                [PasswordField] = PasswordPlaceholder,
            },

            // ──── Passkeys (WebAuthn) ────
            [typeof(AccountPasskeyLoginRequest)] = new JsonObject
            {
                ["credentialJson"] = "{\"id\":\"credential-id\",\"rawId\":\"...\",\"type\":\"public-key\",\"response\":{\"clientDataJSON\":\"...\",\"authenticatorData\":\"...\",\"signature\":\"...\",\"userHandle\":\"...\"}}",
            },
            [typeof(PasskeyRegistrationRequest)] = new JsonObject
            {
                ["credentialJson"] = "{\"id\":\"credential-id\",\"rawId\":\"...\",\"type\":\"public-key\",\"response\":{\"clientDataJSON\":\"...\",\"attestationObject\":\"...\"}}",
                ["name"] = "iPhone 15",
            },
            [typeof(PasskeyRenameRequest)] = new JsonObject
            {
                ["name"] = "MacBook Pro (work)",
            },
        };
}
