using PhoneNumbers;

namespace Granit.Parties.EntityFrameworkCore.Canonicalisation;

/// <summary>
/// Computes the E.164 canonical form (<c>+&lt;country code&gt;&lt;subscriber number&gt;</c>) of a
/// phone number via the libphonenumber-csharp port of Google's libphonenumber. The original
/// input is kept verbatim on <c>PartyPhone.Number</c>; the canonical form lives in the
/// separate <c>PartyPhone.CanonicalNumber</c> column and feeds Tier-1 deterministic
/// duplicate detection (Epic #1280).
/// </summary>
/// <remarks>
/// <para>
/// The UI is expected to capture the country code via a dropdown so input always arrives
/// prefixed with <c>+&lt;cc&gt;</c>. We pass <c>null</c> as the default region to libphonenumber,
/// which means only E.164-prefixed input is accepted — domestic-format numbers (no leading
/// <c>+</c>) cannot be unambiguously parsed without a region.
/// </para>
/// <para>
/// Returns <c>null</c> when libphonenumber cannot parse the input (invalid syntax, missing
/// country code, etc.). The verbatim <c>Number</c> column still preserves what the user
/// typed; only the dedup key is missing in that case, so the row simply does not surface
/// in Tier-1 lookups.
/// </para>
/// </remarks>
public static class PhoneCanonicaliser
{
    private static readonly PhoneNumberUtil _phoneNumberUtil = PhoneNumberUtil.GetInstance();

    /// <summary>
    /// Returns the E.164 form of <paramref name="phone"/>, or <c>null</c> when the input is
    /// null/whitespace or when libphonenumber cannot parse it as a valid number.
    /// </summary>
    public static string? Canonicalise(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        try
        {
            PhoneNumber parsed = _phoneNumberUtil.Parse(phone, defaultRegion: null);
            if (!_phoneNumberUtil.IsValidNumber(parsed))
            {
                return null;
            }

            return _phoneNumberUtil.Format(parsed, PhoneNumberFormat.E164);
        }
        catch (NumberParseException)
        {
            return null;
        }
    }
}
