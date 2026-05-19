namespace Granit.Browsing.Pages;

/// <summary>
/// Asynchronous handler for an intercepted request — internal provider-side
/// collaborator. End-user code subscribes through
/// <see cref="IBrowserPage.RouteAsync(RoutePattern, System.Func{RouteRequest, CancellationToken, ValueTask{RouteDecision}}, CancellationToken)"/>;
/// providers translate the user handler into one of these.
/// </summary>
/// <param name="context">Operation surface tied to the intercepted request.</param>
/// <param name="cancellationToken">Cancellation token.</param>
internal delegate Task RouteHandler(IRouteContext context, CancellationToken cancellationToken);
