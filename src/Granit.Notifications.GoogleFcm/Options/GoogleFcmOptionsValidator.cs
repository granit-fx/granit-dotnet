using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.GoogleFcm.Options;

/// <summary>
/// Validates <see cref="GoogleFcmOptions"/> cross-field rules that DataAnnotations cannot
/// express: the service-account key must be a JSON object of type "service_account", so a
/// mis-pasted secret fails at startup instead of at the first token mint.
/// </summary>
internal sealed class GoogleFcmOptionsValidator : IValidateOptions<GoogleFcmOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, GoogleFcmOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ServiceAccountJson))
        {
            // [Required] already reports the missing value.
            return ValidateOptionsResult.Skip;
        }

        try
        {
            using var doc = JsonDocument.Parse(options.ServiceAccountJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("type", out JsonElement type)
                || type.GetString() != "service_account")
            {
                return ValidateOptionsResult.Fail(
                    "GoogleFcmOptions.ServiceAccountJson must be a Google service-account key (JSON object with \"type\": \"service_account\").");
            }
        }
        catch (JsonException)
        {
            return ValidateOptionsResult.Fail("GoogleFcmOptions.ServiceAccountJson is not valid JSON.");
        }

        return ValidateOptionsResult.Success;
    }
}
