namespace Granit.ReferenceData.Endpoints.Dtos;

/// <summary>
/// Response DTO for a reference data entry. Never exposes the EF entity directly.
/// </summary>
/// <param name="Id">The unique identifier.</param>
/// <param name="Code">The unique business key (e.g., "BE", "EUR").</param>
/// <param name="Label">The culture-resolved display label (based on current UI culture).</param>
/// <param name="LabelEn">English display label.</param>
/// <param name="LabelFr">French display label.</param>
/// <param name="LabelNl">Dutch display label.</param>
/// <param name="LabelDe">German display label.</param>
/// <param name="LabelEs">Spanish display label.</param>
/// <param name="LabelIt">Italian display label.</param>
/// <param name="LabelPt">Portuguese display label.</param>
/// <param name="LabelZh">Chinese display label.</param>
/// <param name="LabelJa">Japanese display label.</param>
/// <param name="LabelPl">Polish display label.</param>
/// <param name="LabelTr">Turkish display label.</param>
/// <param name="LabelKo">Korean display label.</param>
/// <param name="LabelSv">Swedish display label.</param>
/// <param name="LabelCs">Czech display label.</param>
/// <param name="LabelHi">Hindi display label.</param>
/// <param name="Activated">Whether the entry is active.</param>
/// <param name="SortOrder">Display order (lower values first).</param>
/// <param name="ValidFrom">Optional start of validity period.</param>
/// <param name="ValidTo">Optional end of validity period.</param>
/// <param name="ParentCode">Parent code for hierarchical reference data (<c>null</c> for root entries).</param>
/// <param name="ExtraProperties">Additional custom properties (JSON bag + shadow properties merged).</param>
public sealed record ReferenceDataResponse(
    Guid Id,
    string Code,
    string Label,
    string LabelEn,
    string LabelFr,
    string LabelNl,
    string LabelDe,
    string LabelEs,
    string LabelIt,
    string LabelPt,
    string LabelZh,
    string LabelJa,
    string LabelPl,
    string LabelTr,
    string LabelKo,
    string LabelSv,
    string LabelCs,
    string LabelHi,
    bool Activated,
    int SortOrder,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? ParentCode,
    Dictionary<string, string>? ExtraProperties);
