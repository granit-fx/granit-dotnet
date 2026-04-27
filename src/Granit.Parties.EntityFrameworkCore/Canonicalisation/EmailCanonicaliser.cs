namespace Granit.Parties.EntityFrameworkCore.Canonicalisation;

/// <summary>
/// Computes the dedup-friendly canonical form of an email address. The original input is
/// kept verbatim on <c>PartyEmail.Address</c>; the canonical form lives in the separate
/// <c>PartyEmail.CanonicalEmail</c> column and feeds Tier-1 deterministic duplicate
/// detection (see Epic #1280).
/// </summary>
/// <remarks>
/// <para>Rules applied:</para>
/// <list type="bullet">
///   <item>trim + <c>ToLowerInvariant</c> on the whole address (case- and whitespace-insensitive)</item>
///   <item>Gmail / Googlemail-specific: strip dots from the local part (<c>j.f.smith</c> ≡ <c>jfsmith</c>)</item>
///   <item>Gmail / Googlemail-specific: strip the <c>+tag</c> suffix from the local part
///         (<c>alice+billing@gmail.com</c> ≡ <c>alice@gmail.com</c>)</item>
///   <item>Domain <c>googlemail.com</c> rewritten to <c>gmail.com</c> (Google treats them
///         as the same provider for delivery)</item>
/// </list>
/// <para>
/// Returns <c>null</c> for null/whitespace input or any address that does not contain
/// exactly one <c>@</c>. Other providers (Outlook, Yahoo, ProtonMail, …) are NOT
/// dot-stripped — only Gmail has the documented "dots are ignored" semantics. The
/// plus-tag rule applies only to Gmail in v1; Outlook + plus-tag stripping can land
/// later under the same canonicaliser without a schema change.
/// </para>
/// </remarks>
public static class EmailCanonicaliser
{
    private const string GmailDomain = "gmail.com";
    private const string GoogleMailDomain = "googlemail.com";

    /// <summary>
    /// Returns the canonical form of <paramref name="email"/>, or <c>null</c> when the
    /// input is null/whitespace or syntactically not an address.
    /// </summary>
    public static string? Canonicalise(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        string trimmed = email.Trim().ToLowerInvariant();
        int at = trimmed.IndexOf('@', StringComparison.Ordinal);

        // Reject input without exactly one '@' — caller is expected to have validated
        // syntax already; an invalid address gets no canonical form, no Tier-1 hit.
        if (at <= 0 || at != trimmed.LastIndexOf('@') || at == trimmed.Length - 1)
        {
            return null;
        }

        string local = trimmed[..at];
        string domain = trimmed[(at + 1)..];

        if (domain == GoogleMailDomain)
        {
            domain = GmailDomain;
        }

        if (domain == GmailDomain)
        {
            int plus = local.IndexOf('+', StringComparison.Ordinal);
            if (plus >= 0)
            {
                local = local[..plus];
            }

            local = local.Replace(".", string.Empty, StringComparison.Ordinal);

            // After plus-tag + dot stripping the local part may collapse to empty —
            // treat that as "no canonical form" rather than emitting "@gmail.com".
            if (local.Length == 0)
            {
                return null;
            }
        }

        return $"{local}@{domain}";
    }
}
