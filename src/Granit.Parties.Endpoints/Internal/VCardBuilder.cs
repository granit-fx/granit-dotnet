using System.Globalization;
using System.Text;
using Granit.Parties.Domain;

namespace Granit.Parties.Endpoints.Internal;

/// <summary>
/// Builds an RFC 6350 vCard 4.0 representation of a <see cref="Party"/>. CRLF line
/// endings, properties folded at 75 octets per RFC, mandatory FN + VERSION + UID.
/// </summary>
internal static class VCardBuilder
{
    private const string Version = "4.0";

    public static string Build(Party party, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(party);

        var sb = new StringBuilder();
        AppendLine(sb, "BEGIN:VCARD");
        AppendLine(sb, $"VERSION:{Version}");
        AppendLine(sb, $"UID:urn:uuid:{party.Id}");

        // Mandatory FN — formatted name.
        AppendProperty(sb, "FN", party.Name);

        // Structured N field — best-effort split on whitespace for Individual,
        // empty surname/given for Company / Department.
        if (party.Kind == PartyKind.Individual)
        {
            (string family, string given) = SplitIndividualName(party.Name);
            AppendLine(sb, $"N:{Escape(family)};{Escape(given)};;;");
        }
        else
        {
            AppendLine(sb, $"N:{Escape(party.Name)};;;;");
        }

        // ORG — only meaningful on companies / departments. Tracks the legal name.
        if (party.Kind != PartyKind.Individual)
        {
            AppendProperty(sb, "ORG", party.Name);
        }

        if (!string.IsNullOrWhiteSpace(party.Website))
        {
            AppendProperty(sb, "URL", party.Website);
        }

        if (!string.IsNullOrWhiteSpace(party.Language))
        {
            AppendProperty(sb, "LANG", party.Language);
        }

        if (!string.IsNullOrWhiteSpace(party.Timezone))
        {
            AppendProperty(sb, "TZ", party.Timezone);
        }

        foreach (PartyEmail email in party.Emails)
        {
            string types = email.IsPrimary ? "INTERNET,pref" : "INTERNET";
            AppendLine(sb, $"EMAIL;TYPE={types}:{Escape(email.Address)}");
        }

        foreach (PartyPhone phone in party.Phones)
        {
            string typeToken = phone.Kind switch
            {
                PhoneKind.Mobile => "cell",
                PhoneKind.Work => "work",
                PhoneKind.Home => "home",
                _ => "voice",
            };
            string types = phone.IsPrimary ? $"{typeToken},pref" : typeToken;
            AppendLine(sb, $"TEL;TYPE={types};VALUE=uri:tel:{Escape(phone.Number)}");
        }

        foreach (PartyAddress addr in party.Addresses)
        {
            string typeToken = addr.Kind switch
            {
                AddressKind.Billing => "work",
                AddressKind.Shipping => "work,postal",
                _ => "home",
            };
            string types = addr.IsDefault ? $"{typeToken},pref" : typeToken;

            // ADR structured: pobox;extended;street;locality;region;code;country
            string adr = string.Join(';',
            [
                "",                                 // pobox
                "",                                 // extended
                Escape(addr.Value.Line1 + (string.IsNullOrEmpty(addr.Value.Line2) ? "" : " " + addr.Value.Line2)),
                Escape(addr.Value.City),
                Escape(addr.Value.State ?? string.Empty),
                Escape(addr.Value.PostalCode),
                Escape(addr.Value.Country),
            ]);
            AppendLine(sb, $"ADR;TYPE={types}:{adr}");
        }

        AppendLine(sb, $"REV:{now.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)}");
        AppendLine(sb, "END:VCARD");
        return sb.ToString();
    }

    public static string SuggestedFileName(Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        // Sanitize the party name for cross-OS filesystem compatibility.
        var sb = new StringBuilder(party.Name.Length);
        foreach (char ch in party.Name)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_');
        }

        string slug = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(slug)
            ? $"party-{party.Id:N}.vcf"
            : $"{slug}.vcf";
    }

    private static (string family, string given) SplitIndividualName(string fullName)
    {
        string[] parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[1], parts[0]),
        };
    }

    private static void AppendProperty(StringBuilder sb, string name, string value) =>
        AppendLine(sb, $"{name}:{Escape(value)}");

    private static string Escape(string value) =>
        value
            .Replace("\\", @"\\", StringComparison.Ordinal)
            .Replace(",", @"\,", StringComparison.Ordinal)
            .Replace(";", @"\;", StringComparison.Ordinal)
            .Replace("\r\n", @"\n", StringComparison.Ordinal)
            .Replace("\n", @"\n", StringComparison.Ordinal);

    private static void AppendLine(StringBuilder sb, string line) =>
        sb.Append(line).Append("\r\n");
}
