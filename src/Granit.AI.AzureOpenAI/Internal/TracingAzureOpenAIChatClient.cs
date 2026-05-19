using System.Diagnostics;
using System.Runtime.CompilerServices;
using Granit.AI.AzureOpenAI.Diagnostics;
using Granit.AI.Tenancy;
using Microsoft.Extensions.AI;

namespace Granit.AI.AzureOpenAI.Internal;

/// <summary>
/// Decorator that emits OpenTelemetry GenAI spans around the inner Azure OpenAI <see cref="IChatClient"/>.
/// </summary>
/// <remarks>
/// One span per request — both <c>GetResponseAsync</c> and <c>GetStreamingResponseAsync</c> are wrapped.
/// Disposal is delegated to the wrapped client; the decorator owns no additional resources.
/// </remarks>
internal sealed class TracingAzureOpenAIChatClient(
    IChatClient inner,
    string requestedDeployment,
    AIProviderCredential credential) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return GetResponseCoreAsync(messages, options, cancellationToken);

        async Task<ChatResponse> GetResponseCoreAsync(
            IEnumerable<ChatMessage> m, ChatOptions? o, CancellationToken ct)
        {
            using Activity? activity = StartActivity(AIAzureOpenAIActivitySource.ChatOperation);
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

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using Activity? activity = StartActivity(AIAzureOpenAIActivitySource.ChatStreamOperation);
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

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        inner.GetService(serviceType, serviceKey);

    public void Dispose() => inner.Dispose();

    private Activity? StartActivity(string operationName)
    {
        Activity? activity = AIAzureOpenAIActivitySource.Source.StartActivity(operationName, ActivityKind.Client);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("gen_ai.system", AIAzureOpenAIActivitySource.SystemTagValue);
        activity.SetTag("gen_ai.request.model", requestedDeployment);
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
