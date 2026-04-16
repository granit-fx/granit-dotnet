namespace Granit.AI;

/// <summary>
/// Well-known capability extension identifiers for provider-specific features.
/// Use with <see cref="AIModelCapabilities.Extensions"/>.
/// </summary>
/// <remarks>
/// These constants cover features that are not universally available across providers.
/// Providers may register additional custom extension strings beyond these.
/// </remarks>
public static class WellKnownAICapabilities
{
    /// <summary>The model can search the web for real-time information.</summary>
    public const string WebSearch = "web-search";

    /// <summary>The model can execute code in a sandboxed environment.</summary>
    public const string CodeInterpreter = "code-interpreter";

    /// <summary>The model accepts file uploads as input.</summary>
    public const string FileUpload = "file-upload";

    /// <summary>The model can reference and reason over attached file context.</summary>
    public const string FileContext = "file-context";

    /// <summary>The model can produce inline citations referencing source material.</summary>
    public const string Citations = "citations";

    /// <summary>The model emits status updates during long-running operations.</summary>
    public const string StatusUpdates = "status-updates";

    /// <summary>The model exposes built-in tools managed by the provider.</summary>
    public const string BuiltinTools = "builtin-tools";
}
