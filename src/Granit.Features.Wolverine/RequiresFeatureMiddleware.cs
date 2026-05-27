using Granit.Features.Wolverine.Attributes;

namespace Granit.Features.Wolverine;

/// <summary>
/// Wolverine pipeline middleware that enforces <see cref="RequiresFeatureAttribute"/> checks
/// before the message handler executes.
/// </summary>
/// <remarks>
/// Wolverine discovers this middleware by convention (class with a static <c>BeforeAsync</c> method).
/// Register it in your Wolverine setup:
/// <code>
/// opts.Policies.AddMiddleware&lt;RequiresFeatureMiddleware&gt;(
///     chain => chain.MessageType.HasAttribute&lt;RequiresFeatureAttribute&gt;());
/// </code>
/// When a required feature is disabled, <see cref="Exceptions.FeatureNotEnabledException"/> is thrown
/// and mapped to HTTP 403 by <c>DefaultExceptionStatusCodeMapper</c>.
/// </remarks>
public static class RequiresFeatureMiddleware
{
    /// <summary>
    /// Wolverine "before" hook — invoked before the message handler.
    /// Reads all <see cref="RequiresFeatureAttribute"/> decorating the message type and
    /// calls <see cref="IFeatureChecker.RequireEnabledAsync"/> for each.
    /// </summary>
    /// <param name="message">The incoming message whose type is inspected for attributes.</param>
    /// <param name="featureChecker">Resolved from the DI container by Wolverine.</param>
    /// <param name="cancellationToken">Cancellation token propagated by Wolverine from the transport.</param>
    public static async Task BeforeAsync(
        object message,
        IFeatureChecker featureChecker,
        CancellationToken cancellationToken)
    {
        IEnumerable<RequiresFeatureAttribute> attributes =
            message.GetType()
                   .GetCustomAttributes(typeof(RequiresFeatureAttribute), inherit: true)
                   .Cast<RequiresFeatureAttribute>();

        foreach (RequiresFeatureAttribute attribute in attributes)
        {
            await featureChecker.RequireEnabledAsync(attribute.FeatureName, cancellationToken).ConfigureAwait(false);
        }
    }
}
