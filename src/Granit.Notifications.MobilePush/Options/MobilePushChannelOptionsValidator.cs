using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.MobilePush.Options;

/// <summary>
/// Fails startup when <see cref="MobilePushChannelOptions.Provider"/> does not resolve to a registered keyed
/// <see cref="IMobilePushSender"/> — misconfiguration surfaces at boot with an explicit message
/// instead of a <c>GetRequiredKeyedService</c> throw at the first send.
/// </summary>
internal sealed class MobilePushChannelOptionsValidator(IServiceProviderIsKeyedService keyedServices) : IValidateOptions<MobilePushChannelOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, MobilePushChannelOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            return ValidateOptionsResult.Fail(
                "MobilePushChannelOptions.Provider is required (Notifications:MobilePush:Provider): set it to the key of a registered IMobilePushSender provider.");
        }

        if (!keyedServices.IsKeyedService(typeof(IMobilePushSender), options.Provider))
        {
            return ValidateOptionsResult.Fail(
                $"No IMobilePushSender is registered for provider key '{options.Provider}' " +
                "(Notifications:MobilePush:Provider). Reference the matching provider package (its module " +
                "self-registers) or fix the configured key.");
        }

        return ValidateOptionsResult.Success;
    }
}
