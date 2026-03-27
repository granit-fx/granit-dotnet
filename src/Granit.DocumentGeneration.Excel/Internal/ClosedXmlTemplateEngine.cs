using System.Text.Json;
using ClosedXML.Excel;
using Granit.Templating.GlobalContext;
using Granit.Templating.Pipeline;

// 'DocumentFormat' is a root namespace from DocumentFormat.OpenXml (transitive ClosedXML dep.).
// Use a distinct alias to avoid CS0118/CS0576.
using TemplatingDocFormat = Granit.Templating.Keys.DocumentFormat;

namespace Granit.DocumentGeneration.Excel.Internal;

/// <summary>
/// <see cref="ITemplateEngine"/> implementation backed by
/// <a href="https://closedxml.io/">ClosedXML</a>.
/// Supports <c>application/vnd.openxmlformats-officedocument.spreadsheetml.sheet</c> MIME type.
/// </summary>
/// <remarks>
/// The template content stored in <see cref="TemplateDescriptor.Content"/> must be a
/// Base64-encoded XLSX workbook. The engine decodes it, replaces <c>{{model.property}}</c>
/// placeholders in string cells, and returns a <see cref="BinaryRenderedContent"/> with
/// the completed workbook bytes.
/// <para>
/// Nested objects are flattened with dot notation and arrays with bracket notation:
/// <c>{{model.address.city}}</c>, <c>{{model.lines[0].amount}}</c>.
/// </para>
/// <para>
/// Unlike text engines (Scriban), <c>ClosedXmlTemplateEngine</c> returns binary output
/// directly — no <c>IDocumentRenderer</c> is invoked by <c>DocumentGenerator</c>.
/// </para>
/// </remarks>
internal sealed class ClosedXmlTemplateEngine : ITemplateEngine
{
    private const string ExcelMimeType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <inheritdoc/>
    public bool CanRender(TemplateDescriptor descriptor) =>
        string.Equals(descriptor.MimeType, ExcelMimeType, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public Task<RenderedContent> RenderAsync<TData>(
        TemplateDescriptor descriptor,
        TData data,
        TemplatingDocFormat targetFormat,
        IReadOnlyList<ITemplateGlobalContext> globalContexts,
        CancellationToken cancellationToken = default) where TData : notnull
    {
        byte[] templateBytes = Convert.FromBase64String(descriptor.Content);
        Dictionary<string, string> substitutions = BuildSubstitutions(data);

        using MemoryStream outputStream = new();
        using (MemoryStream inputStream = new(templateBytes))
        using (XLWorkbook workbook = new(inputStream))
        {
            ApplySubstitutions(workbook, substitutions);
            workbook.SaveAs(outputStream);
        }

        RenderedContent result = new BinaryRenderedContent(
            outputStream.ToArray(), TemplatingDocFormat.Excel)
        {
            RevisionId = descriptor.RevisionId,
        };

        return Task.FromResult(result);
    }

    private static Dictionary<string, string> BuildSubstitutions<TData>(TData data)
        where TData : notnull
    {
        JsonElement element = JsonSerializer.SerializeToElement(data, SnakeCaseOptions);
        Dictionary<string, string> subs = [];
        FlattenElement(element, "model", subs);
        return subs;
    }

    private const int MaxNestingDepth = 32;

    private static void FlattenElement(
        JsonElement element, string prefix, Dictionary<string, string> subs, int depth = 0)
    {
        if (depth > MaxNestingDepth)
        {
            throw new InvalidOperationException(
                $"Data structure exceeds maximum nesting depth of {MaxNestingDepth}.");
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    FlattenElement(property.Value, $"{prefix}.{property.Name}", subs, depth + 1);
                }

                break;

            case JsonValueKind.Array:
                int i = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    FlattenElement(item, $"{prefix}[{i++}]", subs, depth + 1);
                }

                break;

            default:
                subs[prefix] = element.ToString();
                break;
        }
    }

    /// <summary>
    /// Characters that trigger formula interpretation in spreadsheet applications.
    /// Matches the protection in <c>CsvExportWriter</c> (CWE-1236 defense-in-depth).
    /// </summary>
    private static readonly char[] FormulaTriggerChars = ['=', '+', '-', '@', '\t', '\r'];

    private static void ApplySubstitutions(
        XLWorkbook workbook, Dictionary<string, string> substitutions)
    {
        // Pre-compute pattern strings once to avoid re-allocation per cell
        List<(string Pattern, string Value)> patterns =
            [.. substitutions.Select(s => ($"{{{{{s.Key}}}}}", s.Value))];

        foreach (IXLWorksheet worksheet in workbook.Worksheets)
        {
            foreach (IXLCell cell in worksheet.CellsUsed())
            {
                if (cell.DataType != XLDataType.Text)
                {
                    continue;
                }

                string value = cell.GetValue<string>();
                foreach ((string pattern, string replacement) in patterns)
                {
                    value = value.Replace(pattern, replacement, StringComparison.Ordinal);
                }

                // Defense-in-depth against formula injection (CWE-1236):
                // ClosedXML's SetValue(string) creates XLDataType.Text cells that are
                // stored as shared strings in XLSX — Excel will NOT interpret them as
                // formulas. The rich-text fallback below provides an additional guard
                // against ClosedXML behavior changes for values starting with formula
                // trigger characters (=, +, -, @, tab, CR).
                if (value.Length > 0 && FormulaTriggerChars.Contains(value[0]))
                {
                    IXLRichText richText = cell.GetRichText();
                    richText.ClearText();
                    richText.AddText(value);
                }
                else
                {
                    cell.SetValue(value);
                }
            }
        }
    }
}
