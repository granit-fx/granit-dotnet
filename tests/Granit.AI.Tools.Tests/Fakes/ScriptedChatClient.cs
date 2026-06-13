using Microsoft.Extensions.AI;

namespace Granit.AI.Tools.Tests.Fakes;

/// <summary>
/// An <see cref="IChatClient"/> that returns a pre-scripted sequence of responses and records
/// the messages it was handed on each call, so a test can assert what was fed back into the loop.
/// </summary>
internal sealed class ScriptedChatClient(params ChatResponse[] responses) : IChatClient
{
    private readonly Queue<ChatResponse> _responses = new(responses);

    /// <summary>A snapshot of the messages passed on each <see cref="GetResponseAsync"/> call.</summary>
    public List<IReadOnlyList<ChatMessage>> Calls { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add([.. messages]);
        return Task.FromResult(_responses.Dequeue());
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Streaming is not used by the orchestration loop tests.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
