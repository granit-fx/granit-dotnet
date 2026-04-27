using Granit.Modularity;
using Granit.Settings;

namespace Granit.Privacy;

/// <summary>
/// Granit module for PIMS privacy abstractions.
/// Registration is done via <c>AddGranitPrivacy()</c> because it requires
/// an <see cref="System.Action{GranitPrivacyBuilder}"/> for data provider
/// and legal document declarations.
/// </summary>
[DependsOn(typeof(GranitSettingsModule))]
public sealed class GranitPrivacyModule : GranitModule;
