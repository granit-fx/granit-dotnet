using Granit.ReferenceData;

namespace Granit.ReferenceData.Endpoints.Dtos;

/// <summary>
/// Request body for creating a new reference data entry.
/// </summary>
/// <param name="Code">Unique business key (e.g., "BE", "EUR").</param>
/// <param name="LabelEn">Default display label (English).</param>
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
/// <param name="SortOrder">Display order (lower values first).</param>
/// <param name="ValidFrom">Optional start of validity period.</param>
/// <param name="ValidTo">Optional end of validity period.</param>
/// <param name="ParentCode">Optional parent code for hierarchical reference data.</param>
/// <param name="ExtraProperties">Optional extra properties (key-value pairs stored in JSON bag).</param>
public sealed record ReferenceDataCreateRequest(
    string Code,
    string LabelEn,
    string LabelFr = "",
    string LabelNl = "",
    string LabelDe = "",
    string LabelEs = "",
    string LabelIt = "",
    string LabelPt = "",
    string LabelZh = "",
    string LabelJa = "",
    string LabelPl = "",
    string LabelTr = "",
    string LabelKo = "",
    string LabelSv = "",
    string LabelCs = "",
    string LabelHi = "",
    int SortOrder = 0,
    DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidTo = null,
    string? ParentCode = null,
    Dictionary<string, string>? ExtraProperties = null) : IReferenceDataMutableFields;
