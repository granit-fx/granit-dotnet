using System.Globalization;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Granit.Templating.Scriban.Internal;

/// <summary>
/// Scriban filter <c>format_bytes</c> that renders a byte count using the largest
/// binary unit (B / KB / MB / GB / TB / PB) that keeps the number readable.
/// </summary>
/// <remarks>
/// <para>Usage in templates:</para>
/// <code>{{ model.usage_bytes | format_bytes }}</code>
/// <code>Storage: {{ model.usage_bytes | format_bytes }} of {{ model.limit_bytes | format_bytes }}</code>
/// <para>
/// Uses 1024-based ("binary") units so 1 KB = 1024 bytes, matching the convention
/// used elsewhere in Granit (<c>BlobUploadRequest.MaxAllowedBytes</c>,
/// <c>GranitDocumentsOptions.DefaultTenantQuotaBytes</c>). Output is rendered with
/// the invariant culture (no thousands separator) and at most one decimal so
/// templates stay locale-independent — wrap with <c>string.format</c> downstream
/// if the host needs locale-aware separators.
/// </para>
/// <para>
/// Negative values are formatted as <c>"-X UNIT"</c>. <c>null</c> renders as the
/// empty string so optional fields don't emit <c>"NaN"</c>.
/// </para>
/// </remarks>
internal sealed class FormatBytesFunction : IScriptCustomFunction
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public object? Invoke(TemplateContext context, ScriptNode? callerContext,
        ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        if (arguments.Count == 0 || arguments[0] is null)
        {
            return string.Empty;
        }

        if (!TryConvertToInt64(arguments[0], out long bytes))
        {
            return arguments[0]?.ToString() ?? string.Empty;
        }

        bool negative = bytes < 0;
        double abs = Math.Abs((double)bytes);
        int unitIndex = 0;
        while (abs >= 1024 && unitIndex < Units.Length - 1)
        {
            abs /= 1024;
            unitIndex++;
        }

        string number = unitIndex == 0
            ? abs.ToString("0", CultureInfo.InvariantCulture)
            : abs.ToString("0.#", CultureInfo.InvariantCulture);
        return $"{(negative ? "-" : string.Empty)}{number} {Units[unitIndex]}";
    }

    public int RequiredParameterCount => 1;

    public int ParameterCount => 1;

    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;

    public Type ReturnType => typeof(string);

    public ScriptParameterInfo GetParameterInfo(int index) => new(typeof(object), "bytes");

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext,
        ScriptArray arguments, ScriptBlockStatement? blockStatement) =>
        new(Invoke(context, callerContext, arguments, blockStatement));

    private static bool TryConvertToInt64(object? value, out long result)
    {
        switch (value)
        {
            case long l: result = l; return true;
            case int i: result = i; return true;
            case short s: result = s; return true;
            case byte b: result = b; return true;
            case ulong ul when ul <= long.MaxValue: result = (long)ul; return true;
            case uint ui: result = ui; return true;
            case double d when !double.IsNaN(d) && !double.IsInfinity(d): result = (long)d; return true;
            case float f when !float.IsNaN(f) && !float.IsInfinity(f): result = (long)f; return true;
            case decimal m: result = (long)m; return true;
            case string str when long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed):
                result = parsed; return true;
            default:
                result = 0;
                return false;
        }
    }
}
