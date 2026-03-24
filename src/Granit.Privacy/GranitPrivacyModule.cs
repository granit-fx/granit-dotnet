using Granit.Modularity;

namespace Granit.Privacy;

/// <summary>
/// Granit module for PIMS privacy abstractions.
/// Registration is done via <c>AddGranitPrivacy()</c> because it requires
/// an <see cref="System.Action{GranitPrivacyBuilder}"/> for data provider
/// and legal document declarations.
/// </summary>
public sealed class GranitPrivacyModule : GranitModule;
