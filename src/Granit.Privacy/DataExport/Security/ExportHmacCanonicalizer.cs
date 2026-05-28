using System.Globalization;
using System.Text;

namespace Granit.Privacy.DataExport.Security;

/// <summary>
/// Deterministic canonicalizer for <see cref="ExportHmacParameters"/> — produces the
/// byte payload signed by every <see cref="IExportHmacSigner"/> implementation.
/// Length-prefixed concatenation prevents canonicalization ambiguity (two distinct
/// inputs hashing to the same bytes via field-boundary collision).
/// </summary>
public static class ExportHmacCanonicalizer
{
    /// <summary>Canonicalises the parameter tuple into the bytes signed by the underlying MAC.</summary>
    public static byte[] Canonicalize(in ExportHmacParameters parameters)
    {
        StringBuilder sb = new(512);
        AppendField(sb, parameters.RequestId.ToString("N", CultureInfo.InvariantCulture));
        AppendField(sb, parameters.SubjectUserId.ToString("N", CultureInfo.InvariantCulture));
        AppendField(sb, parameters.ProviderName);
        AppendField(sb, parameters.FragmentKind);
        AppendField(sb, parameters.SourceContainer);
        AppendField(sb, parameters.SourceBlobId.ToString("N", CultureInfo.InvariantCulture));
        AppendField(sb, parameters.EntryPath);
        AppendField(sb, parameters.ExpiresAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
        return Encoding.UTF8.GetBytes(sb.ToString());

        static void AppendField(StringBuilder sb, string value)
        {
            sb.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            sb.Append(':');
            sb.Append(value);
            sb.Append('|');
        }
    }
}
