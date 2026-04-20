namespace GranitApi;

/// <summary>Response payload for the sample greeting endpoint.</summary>
/// <param name="Message">Human-readable greeting.</param>
/// <param name="Time">The server-side timestamp when the greeting was produced.</param>
public sealed record GreetingResponse(string Message, DateTimeOffset Time);
