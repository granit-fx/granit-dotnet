using System.Collections.Concurrent;

namespace Granit.Http.ODataExposure.Tests.Integration;

/// <summary>
/// Collects every SQL command EF Core executes against the test database
/// so the assertions can verify the framework's tenant filter is composed
/// BEFORE any user-supplied predicate. Wired via
/// <c>DbContextOptionsBuilder.LogTo(...)</c> on the integration's DbContext.
/// </summary>
internal sealed class SqlCaptureSink
{
    private readonly ConcurrentQueue<string> _commands = new();

    public IReadOnlyList<string> Commands => [.. _commands];

    public void Capture(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return;
        }

        _commands.Enqueue(commandText);
    }

    public void Clear()
    {
        while (_commands.TryDequeue(out _))
        {
            // drain
        }
    }
}
