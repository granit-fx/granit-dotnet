using Granit.Contacts.Privacy.DataExport;
using Granit.Privacy;

namespace Granit.Contacts.Privacy.Extensions;

/// <summary>
/// <see cref="GranitPrivacyBuilder"/> extensions that register the contacts privacy provider.
/// </summary>
public static class PrivacyBuilderContactsExtensions
{
    /// <summary>
    /// Registers <see cref="ContactsPrivacyDataProvider"/> and adds <c>"contacts"</c>
    /// to the scatter-gather registry.
    /// </summary>
    /// <remarks>
    /// The matching Wolverine handler is discovered automatically. Requires
    /// <see cref="GranitContactsPrivacyModule"/> to be loaded.
    /// </remarks>
    public static GranitPrivacyBuilder AddGranitContactsPrivacyProvider(
        this GranitPrivacyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddDataProvider<ContactsPrivacyDataProvider>();
    }
}
