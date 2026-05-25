using Granit.Modularity;

namespace Granit.Html;

/// <summary>
/// Granit module exposing the <c>Granit.Html</c> abstractions. Marker module — registers
/// no services itself; a concrete provider module (e.g. <c>GranitHtmlAngleSharpModule</c>)
/// supplies the implementation.
/// </summary>
public sealed class GranitHtmlModule : GranitModule;
