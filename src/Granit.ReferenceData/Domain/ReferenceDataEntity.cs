using System.Globalization;

using Granit.Domain;

namespace Granit.ReferenceData.Domain;

/// <summary>
/// Abstract base class for all reference data entities (countries, currencies, languages, etc.).
/// Inherits ISO 27001 audit trail from <see cref="AuditedEntity"/> and participates in the
/// <see cref="IActive"/> global query filter registered by <c>ApplyGranitConventions()</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each reference data entity has a unique <see cref="Code"/> (business key) and labels
/// for the 14 supported locales (en, fr, nl, de, es, it, pt, zh, ja, pl, tr, ko, sv, cs).
/// The virtual <see cref="Label"/> property resolves the appropriate label based on
/// <see cref="CultureInfo.CurrentUICulture"/>, falling back to <see cref="LabelEn"/>
/// when the requested locale has no value.
/// Derived entities can override <see cref="Label"/> for custom resolution logic.
/// </para>
/// <para>
/// Soft activation/deactivation is controlled by <see cref="IsActive"/>. Deactivated entries
/// are filtered out by the EF Core global query filter unless explicitly disabled.
/// </para>
/// <para>
/// Implements <see cref="IHasExtraProperties"/> to support application-level extensibility
/// via a JSON property bag. Properties can be promoted to real SQL columns via
/// <c>ExtraPropertyMappingOptions&lt;T&gt;.MapProperty()</c> for indexing and querying.
/// </para>
/// </remarks>
public abstract class ReferenceDataEntity : AuditedEntity, IActive, IHasExtraProperties, IEmitEntityLifecycleEvents
{
    /// <summary>
    /// Unique business key for the reference data entry (e.g., "BE", "EUR", "fr").
    /// Immutable after creation. Used as the lookup key in APIs and seeders.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// English display label (fallback). Always persisted; used when no translation
    /// is available for the requested culture.
    /// </summary>
    public string LabelEn { get; set; } = string.Empty;

    /// <summary>French display label.</summary>
    public string LabelFr { get; set; } = string.Empty;

    /// <summary>Dutch display label.</summary>
    public string LabelNl { get; set; } = string.Empty;

    /// <summary>German display label.</summary>
    public string LabelDe { get; set; } = string.Empty;

    /// <summary>Spanish display label.</summary>
    public string LabelEs { get; set; } = string.Empty;

    /// <summary>Italian display label.</summary>
    public string LabelIt { get; set; } = string.Empty;

    /// <summary>Portuguese display label.</summary>
    public string LabelPt { get; set; } = string.Empty;

    /// <summary>Chinese display label.</summary>
    public string LabelZh { get; set; } = string.Empty;

    /// <summary>Japanese display label.</summary>
    public string LabelJa { get; set; } = string.Empty;

    /// <summary>Polish display label.</summary>
    public string LabelPl { get; set; } = string.Empty;

    /// <summary>Turkish display label.</summary>
    public string LabelTr { get; set; } = string.Empty;

    /// <summary>Korean display label.</summary>
    public string LabelKo { get; set; } = string.Empty;

    /// <summary>Swedish display label.</summary>
    public string LabelSv { get; set; } = string.Empty;

    /// <summary>Czech display label.</summary>
    public string LabelCs { get; set; } = string.Empty;

    /// <summary>
    /// Resolved display label for the current UI culture. Returns the locale-specific
    /// label when available, falling back to <see cref="LabelEn"/> when the translation
    /// is empty or the culture is not in the supported set.
    /// </summary>
    /// <remarks>
    /// This property is not mapped to the database. It is intended for use in application
    /// code and API responses where culture-specific labels are needed.
    /// Override in derived entities to provide custom resolution logic.
    /// </remarks>
    public virtual string Label => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
    {
        "fr" when LabelFr.Length > 0 => LabelFr,
        "nl" when LabelNl.Length > 0 => LabelNl,
        "de" when LabelDe.Length > 0 => LabelDe,
        "es" when LabelEs.Length > 0 => LabelEs,
        "it" when LabelIt.Length > 0 => LabelIt,
        "pt" when LabelPt.Length > 0 => LabelPt,
        "zh" when LabelZh.Length > 0 => LabelZh,
        "ja" when LabelJa.Length > 0 => LabelJa,
        "pl" when LabelPl.Length > 0 => LabelPl,
        "tr" when LabelTr.Length > 0 => LabelTr,
        "ko" when LabelKo.Length > 0 => LabelKo,
        "sv" when LabelSv.Length > 0 => LabelSv,
        "cs" when LabelCs.Length > 0 => LabelCs,
        _ => LabelEn,
    };

    /// <inheritdoc/>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Display order for UI sorting. Lower values appear first. Default is 0.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Optional start date for the validity period. <c>null</c> means valid since the beginning of time.
    /// </summary>
    public DateTimeOffset? ValidFrom { get; set; }

    /// <summary>
    /// Optional end date for the validity period. <c>null</c> means valid indefinitely.
    /// </summary>
    public DateTimeOffset? ValidTo { get; set; }

    /// <inheritdoc/>
    public string? ExtraPropertiesJson { get; set; }

    /// <summary>
    /// Optional parent code for hierarchical reference data (e.g., regions → countries).
    /// <c>null</c> means the entry is a root node.
    /// </summary>
    public string? ParentCode { get; set; }
}
