namespace Granit.Features.Wolverine.Attributes;

/// <summary>
/// Declares that the decorated Wolverine message type requires a specific feature to be enabled
/// for the current tenant/plan context.
/// </summary>
/// <remarks>
/// <para>
/// Decorate the message class and register <see cref="RequiresFeatureMiddleware"/> in your
/// Wolverine setup:
/// <code>
/// [RequiresFeature(AcmeFeatures.ExportPdf.Name)]
/// public sealed record GenerateExportCommand(Guid Id);
/// </code>
/// </para>
/// <para>
/// When the feature is disabled, <see cref="Granit.Features.Exceptions.FeatureNotEnabledException"/>
/// is thrown and mapped to HTTP 403 with <c>errorCode: "Features:NotEnabled"</c>.
/// </para>
/// <para>
/// For HTTP endpoints use the Minimal-API <c>.RequiresFeature("name")</c> filter from
/// <c>Granit.Http.Features</c> instead.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequiresFeatureAttribute(string featureName) : Attribute
{
    /// <summary>The feature that must be enabled for the decorated handler to execute.</summary>
    public string FeatureName { get; } = featureName;
}
