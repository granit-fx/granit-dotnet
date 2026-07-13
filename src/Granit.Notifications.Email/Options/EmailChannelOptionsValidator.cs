using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Email.Options;

/// <summary>
/// Fails startup when <see cref="EmailChannelOptions.Provider"/> does not resolve to a registered keyed
/// <see cref="IEmailSender"/> — misconfiguration surfaces at boot with an explicit message
/// instead of a <c>GetRequiredKeyedService</c> throw at the first send.
/// </summary>
internal sealed class EmailChannelOptionsValidator(IServiceProviderIsKeyedService keyedServices) : IValidateOptions<EmailChannelOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, EmailChannelOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            return ValidateOptionsResult.Fail(
                "EmailChannelOptions.Provider is required (Notifications:Email:Provider): set it to the key of a registered IEmailSender provider.");
        }

        if (!keyedServices.IsKeyedService(typeof(IEmailSender), options.Provider))
        {
            return ValidateOptionsResult.Fail(
                $"No IEmailSender is registered for provider key '{options.Provider}' " +
                "(Notifications:Email:Provider). Reference the matching provider package (its module " +
                "self-registers) or fix the configured key.");
        }

        return ValidateOptionsResult.Success;
    }
}
