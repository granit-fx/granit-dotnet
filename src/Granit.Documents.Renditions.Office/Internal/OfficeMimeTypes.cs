using System;
using System.Collections.Generic;

namespace Granit.Documents.Renditions.Office.Internal;

/// <summary>
/// Office MIME types handled by <see cref="OfficeRenditionProvider"/>. Each entry maps
/// to the file extension expected by <c>soffice --convert-to pdf</c> (LibreOffice infers
/// the input format from the extension when reading from disk).
/// </summary>
internal static class OfficeMimeTypes
{
    public static readonly IReadOnlyDictionary<string, string> ExtensionByMime =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = "docx",
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = "xlsx",
            ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = "pptx",
            ["application/msword"] = "doc",
            ["application/vnd.ms-excel"] = "xls",
            ["application/vnd.ms-powerpoint"] = "ppt",
            ["application/vnd.oasis.opendocument.text"] = "odt",
            ["application/vnd.oasis.opendocument.spreadsheet"] = "ods",
            ["application/vnd.oasis.opendocument.presentation"] = "odp",
            ["application/rtf"] = "rtf",
            ["text/rtf"] = "rtf",
        };
}
