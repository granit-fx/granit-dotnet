using System.Diagnostics.CodeAnalysis;

namespace Granit.Settings.Definitions;

/// <summary>
/// Describes the shape of a <see cref="SettingDefinition"/> value.
/// </summary>
/// <remarks>
/// Values are always persisted and transported as <see cref="string"/>; the kind
/// informs consumers (admin UI, validators, code generators) how to interpret and
/// render them. Orthogonal to <see cref="SettingDefinition.IsEncrypted"/>, which
/// describes at-rest protection, not shape.
/// </remarks>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "ValueKind members intentionally mirror CLR primitive names; renaming would obscure semantics.")]
public enum ValueKind
{
    /// <summary>Free-form string (default).</summary>
    String,

    /// <summary>Boolean parseable as <c>"true"</c> / <c>"false"</c>.</summary>
    Bool,

    /// <summary>Integer parseable by <see cref="int.TryParse(string, out int)"/>.</summary>
    Int,

    /// <summary>Double parseable by <see cref="double.TryParse(string, System.Globalization.NumberStyles, System.IFormatProvider, out double)"/> (invariant culture).</summary>
    Double,

    /// <summary>JSON document parseable by <c>System.Text.Json</c>.</summary>
    Json,
}
