namespace Granit.Http.SecurityHeaders.Csp;

/// <summary>
/// Single source of truth for CSP directive names. Keeps the composer,
/// <see cref="Options.CspOptions"/>, and the builder reading off the same
/// list — drift between these three is what would silently drop a directive
/// from the composed header.
/// </summary>
/// <remarks>
/// Order matters: composer iteration order determines the serialised header's
/// directive order. Most common / highest-precedence directives first so
/// humans scanning the header in DevTools see the important ones quickly.
/// </remarks>
internal static class CspDirectiveNames
{
    public const string DefaultSrc = "default-src";
    public const string ScriptSrc = "script-src";
    public const string ScriptSrcElem = "script-src-elem";
    public const string ScriptSrcAttr = "script-src-attr";
    public const string StyleSrc = "style-src";
    public const string StyleSrcElem = "style-src-elem";
    public const string StyleSrcAttr = "style-src-attr";
    public const string FontSrc = "font-src";
    public const string ImgSrc = "img-src";
    public const string ConnectSrc = "connect-src";
    public const string FrameSrc = "frame-src";
    public const string WorkerSrc = "worker-src";
    public const string MediaSrc = "media-src";
    public const string ObjectSrc = "object-src";
    public const string ManifestSrc = "manifest-src";
    public const string ChildSrc = "child-src";
    public const string BaseUri = "base-uri";
    public const string FormAction = "form-action";
    public const string FrameAncestors = "frame-ancestors";

    /// <summary>
    /// All known directives in serialisation order. Used by the composer to
    /// iterate <see cref="Options.CspOptions"/> + builder state in a single
    /// stable order.
    /// </summary>
    public static readonly string[] All =
    [
        DefaultSrc,
        ScriptSrc, ScriptSrcElem, ScriptSrcAttr,
        StyleSrc, StyleSrcElem, StyleSrcAttr,
        FontSrc, ImgSrc, ConnectSrc,
        FrameSrc, WorkerSrc, MediaSrc, ObjectSrc,
        ManifestSrc, ChildSrc,
        BaseUri, FormAction, FrameAncestors,
    ];
}
