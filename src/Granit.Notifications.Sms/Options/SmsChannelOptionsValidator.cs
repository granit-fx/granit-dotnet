using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Sms.Options;

/// <summary>
/// Fails startup when <see cref="SmsChannelOptions.Provider"/> does not resolve to a registered keyed
/// <see cref="ISmsSender"/> — misconfiguration surfaces at boot with an explicit message
/// instead of a <c>GetRequiredKeyedService</c> throw at the first send.
/// </summary>
internal sealed class SmsChannelOptionsValidator(IServiceProviderIsKeyedService keyedServices) : IValidateOptions<SmsChannelOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SmsChannelOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            return ValidateOptionsResult.Fail(
                "SmsChannelOptions.Provider is required (Notifications:Sms:Provider): set it to the key of a registered ISmsSender provider.");
        }

        if (!keyedServices.IsKeyedService(typeof(ISmsSender), options.Provider))
        {
            return ValidateOptionsResult.Fail(
                $"No ISmsSender is registered for provider key '{options.Provider}' " +
                "(Notifications:Sms:Provider). Reference the matching provider package (its module " +
                "self-registers) or fix the configured key.");
        }

        return ValidateOptionsResult.Success;
    }
}
