using System.Diagnostics;
using System.Diagnostics.Metrics;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Reporting;

namespace Granit.DataExchange.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the data exchange module (import and export).
/// Meter: <c>Granit.DataExchange</c>.
/// </summary>
public sealed class DataExchangeMetrics
{
    public const string MeterName = "Granit.DataExchange";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";
    private const string TagStatus = "status";
    private const string TagDefinition = "definition";
    private const string TagResult = "result";

    // ──── Import instruments ────

    private readonly Counter<long> _importJobsCompleted;
    private readonly Counter<long> _importRowsProcessed;
    private readonly Counter<long> _importRowErrors;
    private readonly Histogram<double> _importDuration;

    // ──── Export instruments ────

    private readonly Counter<long> _exportJobsCompleted;
    private readonly Counter<long> _exportRowsExported;
    private readonly Histogram<double> _exportDuration;

    public DataExchangeMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _importJobsCompleted = meter.CreateCounter<long>(
            "granit.dataexchange.import.jobs.completed",
            description: "Number of import jobs reaching a terminal state.");

        _importRowsProcessed = meter.CreateCounter<long>(
            "granit.dataexchange.import.rows.processed",
            description: "Number of rows processed during import (by result).");

        _importRowErrors = meter.CreateCounter<long>(
            "granit.dataexchange.import.rows.errors",
            description: "Number of row-level errors during import (by kind).");

        _importDuration = meter.CreateHistogram<double>(
            "granit.dataexchange.import.duration",
            unit: "s",
            description: "Duration of import pipeline execution in seconds.");

        _exportJobsCompleted = meter.CreateCounter<long>(
            "granit.dataexchange.export.jobs.completed",
            description: "Number of export jobs reaching a terminal state.");

        _exportRowsExported = meter.CreateCounter<long>(
            "granit.dataexchange.export.rows.exported",
            description: "Number of rows written during export.");

        _exportDuration = meter.CreateHistogram<double>(
            "granit.dataexchange.export.duration",
            unit: "s",
            description: "Duration of export pipeline execution in seconds.");
    }

    /// <summary>
    /// Records metrics for a completed import job (success or failure).
    /// </summary>
    public void RecordImportCompleted(ImportReport report, ImportJob job)
    {
        string tenantId = job.TenantId?.ToString() ?? DefaultTenant;
        string status = report.FinalStatus.ToString();
        string definition = job.DefinitionName;
        string format = NormalizeFormat(job.MimeType);

        _importJobsCompleted.Add(1, new TagList
        {
            { TagTenantId, tenantId },
            { TagStatus, status },
            { TagDefinition, definition },
            { "format", format },
        });

        RecordImportRows(report, tenantId);
        RecordImportErrors(report, tenantId);

        _importDuration.Record(report.Duration.TotalSeconds, new TagList
        {
            { TagTenantId, tenantId },
            { TagStatus, status },
            { TagDefinition, definition },
        });
    }

    /// <summary>
    /// Records metrics for a successfully completed export job.
    /// </summary>
    public void RecordExportCompleted(
        string definitionName, string format, int rowCount, string? tenantId, TimeSpan duration)
    {
        string tenant = tenantId ?? DefaultTenant;

        _exportJobsCompleted.Add(1, new TagList
        {
            { TagTenantId, tenant },
            { TagStatus, "Completed" },
            { TagDefinition, definitionName },
            { "format", format },
        });

        _exportRowsExported.Add(rowCount, new TagList
        {
            { TagTenantId, tenant },
            { TagDefinition, definitionName },
        });

        _exportDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagTenantId, tenant },
            { TagStatus, "Completed" },
            { TagDefinition, definitionName },
        });
    }

    /// <summary>
    /// Records metrics for a failed export job.
    /// </summary>
    public void RecordExportFailed(
        string definitionName, string format, string? tenantId, TimeSpan duration)
    {
        string tenant = tenantId ?? DefaultTenant;

        _exportJobsCompleted.Add(1, new TagList
        {
            { TagTenantId, tenant },
            { TagStatus, "Failed" },
            { TagDefinition, definitionName },
            { "format", format },
        });

        _exportDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagTenantId, tenant },
            { TagStatus, "Failed" },
            { TagDefinition, definitionName },
        });
    }

    private void RecordImportRows(ImportReport report, string tenantId)
    {
        if (report.SucceededRows > 0)
        {
            _importRowsProcessed.Add(report.SucceededRows, new TagList
            {
                { TagTenantId, tenantId },
                { TagResult, "succeeded" },
            });
        }

        if (report.FailedRows > 0)
        {
            _importRowsProcessed.Add(report.FailedRows, new TagList
            {
                { TagTenantId, tenantId },
                { TagResult, "failed" },
            });
        }

        if (report.SkippedRows > 0)
        {
            _importRowsProcessed.Add(report.SkippedRows, new TagList
            {
                { TagTenantId, tenantId },
                { TagResult, "skipped" },
            });
        }

        if (report.InsertedRows > 0)
        {
            _importRowsProcessed.Add(report.InsertedRows, new TagList
            {
                { TagTenantId, tenantId },
                { TagResult, "inserted" },
            });
        }

        if (report.UpdatedRows > 0)
        {
            _importRowsProcessed.Add(report.UpdatedRows, new TagList
            {
                { TagTenantId, tenantId },
                { TagResult, "updated" },
            });
        }
    }

    private void RecordImportErrors(ImportReport report, string tenantId)
    {
        foreach (IGrouping<ImportRowErrorKind, ImportRowError> group in report.RowErrors.GroupBy(e => e.Kind))
        {
            _importRowErrors.Add(group.Count(), new TagList
            {
                { TagTenantId, tenantId },
                { "error_kind", group.Key.ToString().ToLowerInvariant() },
            });
        }
    }

    private static string NormalizeFormat(string mimeType) =>
        mimeType switch
        {
            "text/csv" => "csv",
            _ => "xlsx",
        };
}
