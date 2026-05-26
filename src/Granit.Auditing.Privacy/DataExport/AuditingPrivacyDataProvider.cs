using System.Runtime.CompilerServices;
using Granit.Auditing.Domain;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;

namespace Granit.Auditing.Privacy.DataExport;

/// <summary>
/// Privacy data provider for Granit.Auditing. Emits the user's audit trail (entries they
/// authored, capped by <see cref="AuditExportLimit"/>) as a single staged JSON fragment
/// during the scatter-gather export saga (GDPR Art. 15 — right of access).
/// </summary>
/// <remarks>
/// The cap keeps the fragment bounded so the final archive stays within the privacy size
/// budget. Beyond the cap the assembler's counting stream would abort the entire export
/// with <c>SizeLimitExceeded</c>. Users who hit the cap receive a partial audit history —
/// mirroring what most SAR pipelines do in practice for high-volume trails.
/// </remarks>
public sealed class AuditingPrivacyDataProvider(
    IAuditingReader reader,
    IStagedFragmentBuilder fragmentBuilder) : IPrivacyDataProvider
{
    /// <summary>Maximum audit entries exported for a single user.</summary>
    public const int AuditExportLimit = 10_000;

    /// <inheritdoc />
    public static string ProviderName => "auditing";

    /// <inheritdoc />
    public static string DisplayKey => "Privacy.Scopes.Auditing";

    /// <inheritdoc />
    public static string? FeatureName => null;

    /// <inheritdoc />
    public async ValueTask<bool> HasDataAsync(PrivacyExportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        // Cheap presence check — fetch a single entry instead of the full cap.
        List<AuditEntry> probe = await reader
            .GetByUserAsync(context.SubjectUserId.ToString(), limit: 1, cancellationToken)
            .ConfigureAwait(false);
        return probe.Count > 0;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ExportFragment> ExportAsync(
        PrivacyExportContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<AuditEntry> entries = await reader
            .GetByUserAsync(context.SubjectUserId.ToString(), AuditExportLimit, cancellationToken)
            .ConfigureAwait(false);

        if (entries.Count == 0)
        {
            yield break;
        }

        var dto = new AuditingExportDto(
            UserId: context.SubjectUserId,
            ExportedEntries: entries.Count,
            Truncated: entries.Count == AuditExportLimit,
            Limit: AuditExportLimit,
            Entries: entries.Select(Map).ToList());

        yield return await fragmentBuilder
            .BuildJsonAsync(context, ProviderName, "auditing.json", dto, cancellationToken)
            .ConfigureAwait(false);
    }

    private static AuditingExportEntryDto Map(AuditEntry entry) =>
        new(
            entry.Id,
            entry.Timestamp,
            entry.Category.ToString(),
            entry.UserName,
            entry.IpAddress,
            entry.UserAgent,
            entry.CorrelationId,
            entry.EntityChanges
                .Select(change => new AuditingExportChangeDto(
                    change.EntityType,
                    change.EntityId,
                    change.ChangeType.ToString(),
                    change.PropertyChanges
                        .Select(p => new AuditingExportPropertyDto(p.PropertyName, p.OriginalValue, p.NewValue))
                        .ToList()))
                .ToList());
}

internal sealed record AuditingExportDto(
    Guid UserId,
    int ExportedEntries,
    bool Truncated,
    int Limit,
    IReadOnlyList<AuditingExportEntryDto> Entries);

internal sealed record AuditingExportEntryDto(
    Guid Id,
    DateTimeOffset Timestamp,
    string Category,
    string? UserName,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId,
    IReadOnlyList<AuditingExportChangeDto> EntityChanges);

internal sealed record AuditingExportChangeDto(
    string EntityType,
    string EntityId,
    string ChangeType,
    IReadOnlyList<AuditingExportPropertyDto> PropertyChanges);

internal sealed record AuditingExportPropertyDto(
    string PropertyName,
    string? OriginalValue,
    string? NewValue);
