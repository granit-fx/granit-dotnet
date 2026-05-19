namespace Granit.Browsing.Pages;

/// <summary>
/// Disposition a route handler returns for an intercepted <see cref="RouteRequest"/>.
/// </summary>
/// <remarks>
/// Construct decisions with the static factories — <see cref="Continue"/>,
/// <see cref="Abort"/> or <see cref="Fulfill"/> — never <c>new RouteDecision(...)</c>.
/// The provider inspects <see cref="Kind"/> and the payload fields to dispatch to the
/// engine-native request operation.
/// </remarks>
public sealed record RouteDecision
{
    /// <summary>The kind of decision.</summary>
    public RouteDecisionKind Kind { get; init; }

    /// <summary>Provider-defined error code for <see cref="RouteDecisionKind.Abort"/>; <c>null</c> otherwise.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>HTTP status for <see cref="RouteDecisionKind.Fulfill"/>; <c>0</c> otherwise.</summary>
    public int StatusCode { get; init; }

    /// <summary>Response headers for <see cref="RouteDecisionKind.Fulfill"/>; <c>null</c> otherwise.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>Response body for <see cref="RouteDecisionKind.Fulfill"/>; <c>null</c> otherwise.</summary>
    public byte[]? Body { get; init; }

    /// <summary>Lets the request continue unchanged.</summary>
    public static RouteDecision Continue { get; } = new() { Kind = RouteDecisionKind.Continue };

    /// <summary>Aborts the request with the supplied provider-defined error code.</summary>
    public static RouteDecision Abort(string errorCode = "failed") =>
        new() { Kind = RouteDecisionKind.Abort, ErrorCode = errorCode };

    /// <summary>Fulfills the request with a synthetic response.</summary>
    public static RouteDecision Fulfill(int statusCode, IReadOnlyDictionary<string, string>? headers = null, byte[]? body = null) =>
        new()
        {
            Kind = RouteDecisionKind.Fulfill,
            StatusCode = statusCode,
            Headers = headers,
            Body = body,
        };
}

/// <summary>Kind of <see cref="RouteDecision"/>.</summary>
public enum RouteDecisionKind
{
    /// <summary>Continue the request unchanged.</summary>
    Continue,

    /// <summary>Abort the request.</summary>
    Abort,

    /// <summary>Fulfill the request with a synthetic response.</summary>
    Fulfill,
}
