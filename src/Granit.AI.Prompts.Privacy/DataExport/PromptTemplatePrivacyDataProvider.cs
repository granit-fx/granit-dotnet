using System.Runtime.CompilerServices;
using Granit.AI.Prompts.Domain;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;

namespace Granit.AI.Prompts.Privacy.DataExport;

/// <summary>
/// Privacy data provider for Granit.AI.Prompts. Emits the user's own prompt templates as a single
/// staged JSON fragment during the scatter-gather export saga (GDPR Art. 15/20). Framework-seeded
/// system prompts are owned by no user and are never exported.
/// </summary>
public sealed class PromptTemplatePrivacyDataProvider(
    IPromptTemplateDataManager dataManager,
    IStagedFragmentBuilder fragmentBuilder) : IPrivacyDataProvider
{
    /// <inheritdoc />
    public static string ProviderName => "ai-prompts";

    /// <inheritdoc />
    public static string DisplayKey => "Privacy.Scopes.AIPrompts";

    /// <inheritdoc />
    public static string? FeatureName => null;

    /// <inheritdoc />
    public async ValueTask<bool> HasDataAsync(PrivacyExportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        IReadOnlyList<PromptTemplate> prompts = await dataManager
            .GetAllForOwnerAsync(context.SubjectUserId, cancellationToken).ConfigureAwait(false);
        return prompts.Count > 0;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ExportFragment> ExportAsync(PrivacyExportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExportCoreAsync(context, cancellationToken);
    }

    private async IAsyncEnumerable<ExportFragment> ExportCoreAsync(
        PrivacyExportContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IReadOnlyList<PromptTemplate> prompts = await dataManager
            .GetAllForOwnerAsync(context.SubjectUserId, cancellationToken).ConfigureAwait(false);
        if (prompts.Count == 0)
        {
            yield break;
        }

        var dto = new PromptsExportDto(
            UserId: context.SubjectUserId,
            PromptCount: prompts.Count,
            Prompts: [.. prompts.Select(Map)]);

        yield return await fragmentBuilder
            .BuildJsonAsync(context, ProviderName, "ai-prompts-templates.json", dto, cancellationToken)
            .ConfigureAwait(false);
    }

    private static PromptExportDto Map(PromptTemplate prompt) =>
        new(
            prompt.Id,
            prompt.Name,
            prompt.ShortDescription,
            prompt.Content,
            prompt.Version,
            prompt.CreatedAt,
            [.. prompt.CategoryLinks.Select(l => l.CategoryId)]);
}

internal sealed record PromptsExportDto(
    Guid UserId,
    int PromptCount,
    IReadOnlyList<PromptExportDto> Prompts);

internal sealed record PromptExportDto(
    Guid Id,
    string Name,
    string ShortDescription,
    string Content,
    int Version,
    DateTimeOffset CreatedAt,
    IReadOnlyList<Guid> CategoryIds);
