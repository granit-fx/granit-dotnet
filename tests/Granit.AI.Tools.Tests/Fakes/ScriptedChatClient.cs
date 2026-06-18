using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Granit.AI.Tools.Tests.Fakes;

/// <summary>
/// An <see cref="IChatClient"/> that returns a pre-scripted sequence of responses and records the
/// messages it was handed on each call, so a test can assert what was fed back into the loop. The
/// orchestrator drives the loop through <see cref="FunctionInvokingChatClient"/> in streaming mode,
/// so each scripted response is replayed as a stream of updates.
/// </summary>
internal sealed class ScriptedChatClient(params ChatResponse[] responses) : IChatClient
{
    private readonly Queue<ChatResponse> _responses = new(responses);
    private int _callIndex;

    /// <summary>A snapshot of the messages passed on each call, in order.</summary>
    public List<IReadOnlyList<ChatMessage>> Calls { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add([.. messages]);
        return Task.FromResult(_responses.Dequeue());
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Calls.Add([.. messages]);
        ChatResponse response = _responses.Dequeue();

        // Stamp a distinct response id per model round-trip so the orchestrator can count iterations
        // the way a real provider lets it (each underlying call is one response id).
        response.ResponseId ??= $"resp-{_callIndex++}";
        foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
