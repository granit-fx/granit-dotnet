namespace Granit.AI.Prompting;

/// <summary>
/// Builds the <c>&lt;untrusted_document&gt;</c> instruction-isolation envelope shared by
/// every AI-feature prompt builder, neutralising any attempt to break out of it.
/// </summary>
/// <remarks>
/// The envelope is the framework's first line of defence against OWASP LLM01 prompt
/// injection: a system prompt instructs the model to treat everything inside the element
/// as inert data. That guarantee is only as strong as the envelope boundary — a payload
/// containing a literal <c>&lt;/untrusted_document&gt;</c> would otherwise close the
/// element prematurely and let trailing text pose as out-of-envelope instructions.
/// <see cref="Wrap"/> rewrites embedded open/close tags (case-insensitive) so the single
/// envelope boundary stays where the framework put it. The structured-output schema each
/// detector pins is the second, independent layer.
/// </remarks>
public static class UntrustedDocumentEnvelope
{
    private const string OpenTag = "<untrusted_document>";
    private const string CloseTag = "</untrusted_document>";

    /// <summary>
    /// Wraps <paramref name="content"/> in the <c>&lt;untrusted_document&gt;</c> envelope
    /// after neutralising any embedded envelope tag.
    /// </summary>
    public static string Wrap(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return $"{OpenTag}{Neutralize(content)}{CloseTag}";
    }

    /// <summary>
    /// Rewrites embedded <c>&lt;untrusted_document&gt;</c> / <c>&lt;/untrusted_document&gt;</c>
    /// tags (case-insensitive) so they lose their XML-element meaning. Use directly when a
    /// builder composes the envelope itself; prefer <see cref="Wrap"/> otherwise.
    /// </summary>
    public static string Neutralize(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return content
            .Replace(CloseTag, "</untrusted_document_>", StringComparison.OrdinalIgnoreCase)
            .Replace(OpenTag, "<untrusted_document_>", StringComparison.OrdinalIgnoreCase);
    }
}
