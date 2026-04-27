using System.Text;

namespace Granit.Parties.EntityFrameworkCore.Canonicalisation;

/// <summary>
/// Normalises a tax identifier (VAT number, business registration) to a strip-formatted
/// upper-case form. Used by the EF Core canonicalisation interceptor to overwrite
/// <c>Party.TaxId</c> in place — single column, no separate canonical field, because the
/// canonical form IS the legal one (KBO/BCE, HMRC, IRS, … all accept it without separators).
/// </summary>
/// <remarks>
/// <para>v1 rules (provider-agnostic):</para>
/// <list type="bullet">
///   <item><c>ToUpperInvariant</c></item>
///   <item>Strip ASCII whitespace, dots, hyphens, slashes</item>
///   <item>Trim leading / trailing whitespace</item>
/// </list>
/// <para>
/// Per-country checksum / format validation (Belgian KBO mod-97, French SIRET Luhn, etc.)
/// is deferred to a v2. The Tier-1 dedup only needs structural canonicalisation — two parties
/// with VAT <c>"BE 0123.456.789"</c> and <c>"be0123456789"</c> must collapse to the same key.
/// </para>
/// </remarks>
public static class TaxIdCanonicaliser
{
    /// <summary>
    /// Returns the canonical form of <paramref name="taxId"/>, or <c>null</c> when the input
    /// is null/whitespace, or when stripping separators leaves nothing left.
    /// </summary>
    public static string? Canonicalise(string? taxId)
    {
        if (string.IsNullOrWhiteSpace(taxId))
        {
            return null;
        }

        StringBuilder sb = new(taxId.Length);
        foreach (char c in taxId)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToUpperInvariant(c));
            }
        }

        return sb.Length == 0 ? null : sb.ToString();
    }
}
