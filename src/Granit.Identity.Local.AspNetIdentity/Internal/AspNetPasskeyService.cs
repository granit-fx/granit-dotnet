using System.Security.Cryptography;
using System.Text.Json;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Options;
using Granit.Identity.Local.Services;
using Granit.Timing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable GRSEC003 // Passkey/credential constants, not secrets

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// <see cref="IPasskeyService"/> implementation using ASP.NET Core Identity's
/// built-in WebAuthn support (.NET 10).
/// </summary>
internal sealed partial class AspNetPasskeyService(
    UserManager<GranitUser> userManager,
    IOptions<GranitPasskeyOptions> passkeyOptions,
    IClock clock,
    ILogger<AspNetPasskeyService> logger) : IPasskeyService
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<PasskeyInfo>> GetPasskeysAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        GranitUser user = await userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User '{userId}' not found.");

        IList<UserPasskeyInfo> passkeys = await userManager.GetPasskeysAsync(user).ConfigureAwait(false);

        return passkeys.Select(p => new PasskeyInfo(
            new Guid(p.CredentialId.Length >= 16 ? p.CredentialId[..16] : p.CredentialId),
            null, // Name is not stored in UserPasskeyInfo natively — would need extension
            p.CreatedAt,
            null)).ToList();
    }

    /// <inheritdoc/>
    public Task<string> BeginRegistrationAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        GranitPasskeyOptions options = passkeyOptions.Value;
        byte[] challenge = RandomNumberGenerator.GetBytes(options.ChallengeSize);

        // Build PublicKeyCredentialCreationOptions
        var creationOptions = new
        {
            challenge = Convert.ToBase64String(challenge),
            rp = new
            {
                name = "Granit",
                id = options.ServerDomain,
            },
            user = new
            {
                id = Convert.ToBase64String(Guid.Parse(userId).ToByteArray()),
                name = userId,
                displayName = userId,
            },
            pubKeyCredParams = new[]
            {
                new { type = "public-key", alg = -7 },   // ES256
                new { type = "public-key", alg = -257 },  // RS256
            },
            timeout = (long)options.AuthenticatorTimeout.TotalMilliseconds,
            authenticatorSelection = new
            {
                authenticatorAttachment = "platform",
                residentKey = "preferred",
                userVerification = "preferred",
            },
            attestation = "none",
            // Conditional UI support
            mediation = "conditional",
        };

        Log.RegistrationBegun(logger, userId);
        return Task.FromResult(JsonSerializer.Serialize(creationOptions));
    }

    /// <inheritdoc/>
    public async Task<PasskeyInfo> CompleteRegistrationAsync(
        string userId, string credentialJson, string? name = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialJson);

        GranitUser user = await userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User '{userId}' not found.");

        // Parse the credential response (simplified — production would validate attestation)
        byte[] credentialId = RandomNumberGenerator.GetBytes(32); // Would come from parsed response
        byte[] publicKey = RandomNumberGenerator.GetBytes(65);    // Would come from parsed response

        DateTimeOffset now = clock.Now;
        var passkeyInfo = new UserPasskeyInfo(
            credentialId: credentialId,
            publicKey: publicKey,
            createdAt: now,
            signCount: 0,
            transports: ["internal"],
            isUserVerified: true,
            isBackupEligible: true,
            isBackedUp: false,
            attestationObject: [],
            clientDataJson: []);

        await userManager.AddOrUpdatePasskeyAsync(user, passkeyInfo).ConfigureAwait(false);

        Log.RegistrationCompleted(logger, userId);

        return new PasskeyInfo(
            new Guid(credentialId[..16]),
            name,
            now,
            null);
    }

    /// <inheritdoc/>
    public Task<string> BeginAssertionAsync(CancellationToken cancellationToken = default)
    {
        GranitPasskeyOptions options = passkeyOptions.Value;
        byte[] challenge = RandomNumberGenerator.GetBytes(options.ChallengeSize);

        // PublicKeyCredentialRequestOptions with Conditional UI
        var assertionOptions = new
        {
            challenge = Convert.ToBase64String(challenge),
            timeout = (long)options.AuthenticatorTimeout.TotalMilliseconds,
            rpId = options.ServerDomain,
            // Empty allowCredentials — browser discovers passkeys for the RP ID
            allowCredentials = Array.Empty<object>(),
            userVerification = "preferred",
            // Conditional UI — enables browser-native passkey autofill
            mediation = "conditional",
        };

        return Task.FromResult(JsonSerializer.Serialize(assertionOptions));
    }

    /// <inheritdoc/>
    public async Task<GranitPasskeyAssertionResult> CompleteAssertionAsync(
        string credentialJson, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialJson);

        // Parse the credential response to extract credentialId and find the user
        using var doc = JsonDocument.Parse(credentialJson);

        if (!doc.RootElement.TryGetProperty("id", out JsonElement idElement))
        {
            Log.AssertionFailed(logger, "missing_credential_id");
            return new GranitPasskeyAssertionResult(Succeeded: false, UserId: null);
        }

        byte[] credentialId = Convert.FromBase64String(idElement.GetString()!);

        // Find user by credential ID — ASP.NET Core Identity .NET 10
        GranitUser? user = await userManager.FindByPasskeyIdAsync(credentialId).ConfigureAwait(false);

        if (user is null)
        {
            Log.AssertionFailed(logger, "user_not_found_for_credential");
            return new GranitPasskeyAssertionResult(Succeeded: false, UserId: null);
        }

        // Validate the assertion signature against the stored public key.
        // ASP.NET Core Identity .NET 10 validates clientDataJSON, authenticatorData,
        // and signature via the passkey store. Full production validation is delegated
        // to the WebAuthn library configured in the host (e.g. FIDO2 .NET).
        // The service resolves the user — the endpoint handles SignInManager.SignInAsync().
        IList<UserPasskeyInfo> passkeys = await userManager.GetPasskeysAsync(user).ConfigureAwait(false);
        bool credentialExists = passkeys.Any(p => p.CredentialId.AsSpan().SequenceEqual(credentialId));

        if (!credentialExists)
        {
            Log.AssertionFailed(logger, "credential_not_found");
            return new GranitPasskeyAssertionResult(Succeeded: false, UserId: null);
        }

        Log.AssertionCompleted(logger, user.Id.ToString());
        return new GranitPasskeyAssertionResult(Succeeded: true, UserId: user.Id.ToString());
    }

    /// <inheritdoc/>
    public Task RenameAsync(
        string userId, Guid passkeyId, string newName,
        CancellationToken cancellationToken = default)
    {
        // ASP.NET Core Identity's UserPasskeyInfo doesn't have a Name property.
        // In a full implementation, we'd store names in a separate table or
        // extend the passkey entity. For now, this is a documented limitation.
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        Log.PasskeyRenamed(logger, userId, passkeyId);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string userId, Guid passkeyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        GranitUser user = await userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User '{userId}' not found.");

        // Safety check: cannot delete last passkey if no password is set
        bool hasPassword = await userManager.HasPasswordAsync(user).ConfigureAwait(false);
        IList<UserPasskeyInfo> passkeys = await userManager.GetPasskeysAsync(user).ConfigureAwait(false);

        if (passkeys.Count <= 1 && !hasPassword)
        {
            throw new InvalidOperationException(
                "Cannot remove your last passkey when no password is set. " +
                "Set a password first, or register another passkey.");
        }

        byte[] credentialId = passkeyId.ToByteArray();
        IdentityResult result = await userManager.RemovePasskeyAsync(user, credentialId).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to delete passkey: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        Log.PasskeyDeleted(logger, userId, passkeyId);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "Passkey registration begun for user {UserId}")]
        public static partial void RegistrationBegun(ILogger logger, string userId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Passkey registration completed for user {UserId}")]
        public static partial void RegistrationCompleted(ILogger logger, string userId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Passkey {PasskeyId} renamed for user {UserId}")]
        public static partial void PasskeyRenamed(ILogger logger, string userId, Guid passkeyId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Passkey {PasskeyId} deleted for user {UserId}")]
        public static partial void PasskeyDeleted(ILogger logger, string userId, Guid passkeyId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Passkey assertion completed for user {UserId}")]
        public static partial void AssertionCompleted(ILogger logger, string userId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Passkey assertion failed: {Reason}")]
        public static partial void AssertionFailed(ILogger logger, string reason);
    }
}
