using Anthropic;
using Granit.AI.Anthropic.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Granit.AI.Anthropic.Internal;

/// <summary>
/// Anthropic implementation of <see cref="IAIProviderFactory"/>.
/// </summary>
/// <remarks>
/// Creates <see cref="IChatClient"/> instances backed by the Anthropic SDK.
/// Embedding generation is not supported by Anthropic and always returns <c>null</c>.
/// The underlying <see cref="AnthropicClient"/> is created once and reused for the
/// lifetime of the factory. It is disposed when the factory is disposed.
/// </remarks>
internal sealed class AnthropicProviderFactory(IOptions<AnthropicProviderOptions> options)
    : IAIProviderFactory, IDisposable
{
    private readonly AnthropicClient _client = new() { ApiKey = options.Value.ApiKey };

    /// <inheritdoc />
    public string ProviderName => "Anthropic";

    /// <inheritdoc />
    public IChatClient CreateChatClient(AIWorkspace workspace)
    {
        string model = string.IsNullOrWhiteSpace(workspace.Model) ? options.Value.DefaultModel : workspace.Model;
        return _client.AsIChatClient(model);
    }

    /// <inheritdoc />
    /// <returns>Always <c>null</c>. Anthropic does not support embedding generation.</returns>
    public IEmbeddingGenerator<string, Embedding<float>>? CreateEmbeddingGenerator(AIWorkspace workspace) =>
        null;

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();
}
