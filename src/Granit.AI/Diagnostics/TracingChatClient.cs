using System.Diagnostics;
using System.Runtime.CompilerServices;
using Granit.AI.Tenancy;
using Microsoft.Extensions.AI;

namespace Granit.AI.Diagnostics;

/// <summary>
/// Decorator that emits OpenTelemetry GenAI spans around an inner <see cref="IChatClient"/>,
/// shared by every Granit.AI provider via a thin per-provider subclass.
/// </summary>
/// <remarks>
/// One span per request — both <see cref="GetResponseAsync"/> and
/// <see cref="GetStreamingResponseAsync"/> are wrapped. Tags each span with
/// <c>gen_ai.tenant.id</c>, <c>gen_ai.billed_to_tenant.id</c>, and <c>gen_ai.credential.scope</c>
/// so audit and billing can attribute usage even when the Host fallback served the request.
/// Plaintext <c>ApiKey</c> is never recorded. Disposal is delegated to the wrapped client;
/// the decorator owns no additional resources.
/// </remarks>
/// <param name="inner">The chat client being decorated.</param>
/// <param name="requestedModel">The model the caller requested, recorded as <c>gen_ai.request.model</c>.</param>
/// <param name="credential">The resolved provider credential, used for scope and tenant attribution.</param>
/// <param name="profile">The provider's activity source and GenAI span identity.</param>
public abstract class TracingChatClient(
    IChatClient inner,
    string requestedModel,
    AIProviderCredential credential,
    AIChatTracingProfile profile) : IChatClient
{
    /// <inheritdoc />
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return GetResponseCoreAsync(messages, options, cancellationToken);

        async Task<ChatResponse> GetResponseCoreAsync(
            IEnumerable<ChatMessage> m, ChatOptions? o, CancellationToken ct)
        {
            using Activity? activity = StartActivity(profile.ChatOperation);
            try
            {
                ChatResponse response = await inner.GetResponseAsync(m, o, ct).ConfigureAwait(false);
                TagUsage(activity, response.Usage);
                return response;
            }
            catch (Exception ex)
            {
                MarkError(activity, ex);
                throw;
            }
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using Activity? activity = StartActivity(profile.ChatStreamOperation);
        UsageDetails? lastUsage = null;

        IAsyncEnumerator<ChatResponseUpdate> enumerator;
        try
        {
            enumerator = inner.GetStreamingResponseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
        }
        catch (Exception ex)
        {
            MarkError(activity, ex);
            throw;
        }

        try
        {
            while (true)
            {
                ChatResponseUpdate update;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }
                    update = enumerator.Current;
                }
                catch (Exception ex)
                {
                    MarkError(activity, ex);
                    throw;
                }

                foreach (AIContent content in update.Contents)
                {
                    if (content is UsageContent usage)
                    {
                        lastUsage = usage.Details;
                    }
                }

                yield return update;
            }
        }
        finally
        {
            TagUsage(activity, lastUsage);
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceKey is null && serviceType.IsInstanceOfType(this))
        {
            return this;
        }

        return inner.GetService(serviceType, serviceKey);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the decorator. Releases the wrapped <see cref="IChatClient"/>; override to release
    /// resources a provider subclass adds, always calling the base implementation.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> when called from <see cref="Dispose()"/>; <see langword="false"/> from a finalizer.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }
    }

    private Activity? StartActivity(string operationName)
    {
        Activity? activity = profile.Source.StartActivity(operationName, ActivityKind.Client);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("gen_ai.system", profile.SystemTagValue);
        activity.SetTag("gen_ai.request.model", requestedModel);
        activity.SetTag("gen_ai.credential.scope", credential.Scope.ToString());
        if (credential.BilledToTenantId is { } tenantId)
        {
            string idStr = tenantId.ToString();
            activity.SetTag("gen_ai.tenant.id", idStr);
            activity.SetTag("gen_ai.billed_to_tenant.id", idStr);
        }
        return activity;
    }

    private static void TagUsage(Activity? activity, UsageDetails? usage)
    {
        if (activity is null || usage is null)
        {
            return;
        }

        if (usage.InputTokenCount.HasValue)
        {
            activity.SetTag("gen_ai.usage.input_tokens", usage.InputTokenCount.Value);
        }

        if (usage.OutputTokenCount.HasValue)
        {
            activity.SetTag("gen_ai.usage.output_tokens", usage.OutputTokenCount.Value);
        }
    }

    private static void MarkError(Activity? activity, Exception ex)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.SetTag("error.type", ex.GetType().FullName);
    }
}
