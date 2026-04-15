namespace Granit.ReferenceData;

/// <summary>
/// Shared contract for reference data Create and Update request payloads.
/// Enables reusable FluentValidation rules across both operations.
/// </summary>
public interface IReferenceDataMutableFields
{
    /// <summary>Default display label (English).</summary>
    string LabelEn { get; }

    /// <summary>French display label.</summary>
    string LabelFr { get; }

    /// <summary>Dutch display label.</summary>
    string LabelNl { get; }

    /// <summary>German display label.</summary>
    string LabelDe { get; }

    /// <summary>Spanish display label.</summary>
    string LabelEs { get; }

    /// <summary>Italian display label.</summary>
    string LabelIt { get; }

    /// <summary>Portuguese display label.</summary>
    string LabelPt { get; }

    /// <summary>Chinese display label.</summary>
    string LabelZh { get; }

    /// <summary>Japanese display label.</summary>
    string LabelJa { get; }

    /// <summary>Polish display label.</summary>
    string LabelPl { get; }

    /// <summary>Turkish display label.</summary>
    string LabelTr { get; }

    /// <summary>Korean display label.</summary>
    string LabelKo { get; }

    /// <summary>Swedish display label.</summary>
    string LabelSv { get; }

    /// <summary>Czech display label.</summary>
    string LabelCs { get; }

    /// <summary>Hindi display label.</summary>
    string LabelHi { get; }

    /// <summary>Display order (lower values first).</summary>
    int SortOrder { get; }

    /// <summary>Optional start of validity period.</summary>
    DateTimeOffset? ValidFrom { get; }

    /// <summary>Optional end of validity period.</summary>
    DateTimeOffset? ValidTo { get; }

    /// <summary>Optional parent code for hierarchical reference data.</summary>
    string? ParentCode { get; }

    /// <summary>Optional extra properties (key-value pairs stored in JSON bag).</summary>
    Dictionary<string, string>? ExtraProperties { get; }
}
