using Granit.ReferenceData;

namespace Granit.ReferenceData.Endpoints.Dtos;

/// <summary>
/// Request body for updating an existing reference data entry.
/// </summary>
/// <param name="LabelEn">Updated display label (English).</param>
/// <param name="LabelFr">Updated French display label.</param>
/// <param name="LabelNl">Updated Dutch display label.</param>
/// <param name="LabelDe">Updated German display label.</param>
/// <param name="LabelEs">Updated Spanish display label.</param>
/// <param name="LabelIt">Updated Italian display label.</param>
/// <param name="LabelPt">Updated Portuguese display label.</param>
/// <param name="LabelZh">Updated Chinese display label.</param>
/// <param name="LabelJa">Updated Japanese display label.</param>
/// <param name="LabelPl">Updated Polish display label.</param>
/// <param name="LabelTr">Updated Turkish display label.</param>
/// <param name="LabelKo">Updated Korean display label.</param>
/// <param name="LabelSv">Updated Swedish display label.</param>
/// <param name="LabelCs">Updated Czech display label.</param>
/// <param name="LabelHi">Updated Hindi display label.</param>
/// <param name="SortOrder">Updated display order.</param>
/// <param name="IsActive">Updated active status.</param>
/// <param name="ValidFrom">Updated start of validity period.</param>
/// <param name="ValidTo">Updated end of validity period.</param>
/// <param name="ParentCode">Updated parent code for hierarchical reference data.</param>
/// <param name="ExtraProperties">Updated extra properties (key-value pairs stored in JSON bag).</param>
public sealed record ReferenceDataUpdateRequest(
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
    bool IsActive = true,
    DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidTo = null,
    string? ParentCode = null,
    Dictionary<string, string>? ExtraProperties = null) : IReferenceDataMutableFields;
