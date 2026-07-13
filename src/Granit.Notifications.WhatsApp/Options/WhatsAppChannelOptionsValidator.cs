using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.WhatsApp.Options;

/// <summary>
/// Fails startup when <see cref="WhatsAppChannelOptions.Provider"/> does not resolve to a registered keyed
/// <see cref="IWhatsAppSender"/> — misconfiguration surfaces at boot with an explicit message
/// instead of a <c>GetRequiredKeyedService</c> throw at the first send.
/// </summary>
internal sealed class WhatsAppChannelOptionsValidator(IServiceProviderIsKeyedService keyedServices) : IValidateOptions<WhatsAppChannelOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, WhatsAppChannelOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            return ValidateOptionsResult.Fail(
                "WhatsAppChannelOptions.Provider is required (Notifications:WhatsApp:Provider): set it to the key of a registered IWhatsAppSender provider.");
        }

        if (!keyedServices.IsKeyedService(typeof(IWhatsAppSender), options.Provider))
        {
            return ValidateOptionsResult.Fail(
                $"No IWhatsAppSender is registered for provider key '{options.Provider}' " +
                "(Notifications:WhatsApp:Provider). Reference the matching provider package (its module " +
                "self-registers) or fix the configured key.");
        }

        return ValidateOptionsResult.Success;
    }
}
