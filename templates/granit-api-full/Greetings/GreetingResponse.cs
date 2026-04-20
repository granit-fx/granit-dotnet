namespace GranitApiFull.Greetings;

/// <summary>Response payload for the sample greeting endpoints.</summary>
/// <param name="Message">The greeting message.</param>
/// <param name="Time">Server-side timestamp when the greeting was produced.</param>
public sealed record GreetingResponse(string Message, DateTimeOffset Time);
