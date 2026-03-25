using System.Diagnostics.Metrics;
using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Reporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Diagnostics;

public sealed class DataExchangeMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IMeterFactory _meterFactory;
    private readonly DataExchangeMetrics _metrics;

    public DataExchangeMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new DataExchangeMetrics(_meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void RecordImportCompleted_WithSuccessReport_RecordsJobAndRows()
    {
        // Arrange
        using var jobCollector = new MetricCollector<long>(
            _meterFactory, DataExchangeMetrics.MeterName, "granit.data_exchange.import.jobs.completed");
        using var rowCollector = new MetricCollector<long>(
            _meterFactory, DataExchangeMetrics.MeterName, "granit.data_exchange.import.rows.processed");
        using var durationCollector = new MetricCollector<double>(
            _meterFactory, DataExchangeMetrics.MeterName, "granit.data_exchange.import.duration");

        var job = ImportJob.Create(
            Guid.NewGuid(), "Acme.PatientImport", "Patient",
            "patients.csv", "text/csv", 1024, "blob-ref", Guid.NewGuid());
        ImportReport report = new()
        {
            TotalRows = 100,
            SucceededRows = 95,
            FailedRows = 3,
            SkippedRows = 2,
            InsertedRows = 80,
            UpdatedRows = 15,
            Duration = TimeSpan.FromSeconds(5.5),
            FinalStatus = ImportJobStatus.Completed,
            RowErrors = [],
        };

        // Act
        _metrics.RecordImportCompleted(report, job);

        // Assert
        IReadOnlyList<CollectedMeasurement<long>> jobs = jobCollector.GetMeasurementSnapshot();
        jobs.ShouldHaveSingleItem();
        jobs[0].Value.ShouldBe(1);
        jobs[0].Tags["status"].ShouldBe("Completed");
        jobs[0].Tags["definition"].ShouldBe("Acme.PatientImport");
        jobs[0].Tags["format"].ShouldBe("csv");

        IReadOnlyList<CollectedMeasurement<long>> rows = rowCollector.GetMeasurementSnapshot();
        rows.Count.ShouldBeGreaterThanOrEqualTo(4); // succeeded, failed, skipped, inserted, updated (non-zero)

        IReadOnlyList<CollectedMeasurement<double>> durations = durationCollector.GetMeasurementSnapshot();
        durations.ShouldHaveSingleItem();
        durations[0].Value.ShouldBe(5.5, 0.01);
    }

    [Fact]
    public void RecordImportCompleted_WithFailedReport_RecordsFailedStatus()
    {
        // Arrange
        using var jobCollector = new MetricCollector<long>(
            _meterFactory, DataExchangeMetrics.MeterName, "granit.data_exchange.import.jobs.completed");

        var job = ImportJob.Create(
            Guid.NewGuid(), "Acme.Import", "Entity",
            "data.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            2048, "blob-ref");
        ImportReport report = new()
        {
            TotalRows = 0,
            SucceededRows = 0,
            FailedRows = 0,
            SkippedRows = 0,
            InsertedRows = 0,
            UpdatedRows = 0,
            Duration = TimeSpan.FromSeconds(1.2),
            FinalStatus = ImportJobStatus.Failed,
            RowErrors =
            [
                new ImportRowError(0, ImportRowErrorKind.Persistence, ["Granit:Error"], "Pipeline error"),
            ],
        };

        // Act
        _metrics.RecordImportCompleted(report, job);

        // Assert
        IReadOnlyList<CollectedMeasurement<long>> jobs = jobCollector.GetMeasurementSnapshot();
        jobs.ShouldHaveSingleItem();
        jobs[0].Tags["status"].ShouldBe("Failed");
        jobs[0].Tags["format"].ShouldBe("xlsx");
        jobs[0].Tags["tenant_id"].ShouldBe("global");
    }

    [Fact]
    public void RecordImportCompleted_WithMixedErrors_RecordsAllErrorKinds()
    {
        // Arrange
        using var errorCollector = new MetricCollector<long>(
            _meterFactory, DataExchangeMetrics.MeterName, "granit.data_exchange.import.rows.errors");

        var job = ImportJob.Create(
            Guid.NewGuid(), "Acme.Import", "Entity",
            "data.csv", "text/csv", 1024, "blob-ref", Guid.NewGuid());
        ImportReport report = new()
        {
            TotalRows = 10,
            SucceededRows = 5,
            FailedRows = 5,
            SkippedRows = 0,
            InsertedRows = 5,
            UpdatedRows = 0,
            Duration = TimeSpan.FromSeconds(2),
            FinalStatus = ImportJobStatus.PartiallyCompleted,
            RowErrors =
            [
                new ImportRowError(1, ImportRowErrorKind.Conversion, ["E1"], "Bad type"),
                new ImportRowError(2, ImportRowErrorKind.Conversion, ["E1"], "Bad type"),
                new ImportRowError(3, ImportRowErrorKind.Validation, ["E2"], "Required"),
                new ImportRowError(4, ImportRowErrorKind.Persistence, ["E3"], "Constraint"),
                new ImportRowError(5, ImportRowErrorKind.Identity, ["E4"], "Ambiguous"),
            ],
        };

        // Act
        _metrics.RecordImportCompleted(report, job);

        // Assert
        IReadOnlyList<CollectedMeasurement<long>> errors = errorCollector.GetMeasurementSnapshot();
        errors.Count.ShouldBe(4); // 4 distinct error kinds
        errors.ShouldContain(m => m.Tags["error_kind"]!.ToString() == "conversion" && m.Value == 2);
        errors.ShouldContain(m => m.Tags["error_kind"]!.ToString() == "validation" && m.Value == 1);
        errors.ShouldContain(m => m.Tags["error_kind"]!.ToString() == "persistence" && m.Value == 1);
        errors.ShouldContain(m => m.Tags["error_kind"]!.ToString() == "identity" && m.Value == 1);
    }

    [Fact]
    public void RecordExportCompleted_RecordsJobRowsAndDuration()
    {
        // Arrange
        using var jobCollector = new MetricCollector<long>(
            _meterFactory, DataExchangeMetrics.MeterName, "granit.data_exchange.export.jobs.completed");
        using var rowCollector = new MetricCollector<long>(
            _meterFactory, DataExchangeMetrics.MeterName, "granit.data_exchange.export.rows.exported");
        using var durationCollector = new MetricCollector<double>(
            _meterFactory, DataExchangeMetrics.MeterName, "granit.data_exchange.export.duration");

        // Act
        _metrics.RecordExportCompleted("Acme.PatientExport", "xlsx", 250, "tenant-abc", TimeSpan.FromSeconds(3.7));

        // Assert
        IReadOnlyList<CollectedMeasurement<long>> jobs = jobCollector.GetMeasurementSnapshot();
        jobs.ShouldHaveSingleItem();
        jobs[0].Tags["status"].ShouldBe("Completed");
        jobs[0].Tags["definition"].ShouldBe("Acme.PatientExport");
        jobs[0].Tags["format"].ShouldBe("xlsx");
        jobs[0].Tags["tenant_id"].ShouldBe("tenant-abc");

        IReadOnlyList<CollectedMeasurement<long>> rows = rowCollector.GetMeasurementSnapshot();
        rows.ShouldHaveSingleItem();
        rows[0].Value.ShouldBe(250);

        IReadOnlyList<CollectedMeasurement<double>> durations = durationCollector.GetMeasurementSnapshot();
        durations.ShouldHaveSingleItem();
        durations[0].Value.ShouldBe(3.7, 0.01);
    }

    [Fact]
    public void RecordExportFailed_RecordsFailedJobAndDuration()
    {
        // Arrange
        using var jobCollector = new MetricCollector<long>(
            _meterFactory, DataExchangeMetrics.MeterName, "granit.data_exchange.export.jobs.completed");
        using var durationCollector = new MetricCollector<double>(
            _meterFactory, DataExchangeMetrics.MeterName, "granit.data_exchange.export.duration");

        // Act
        _metrics.RecordExportFailed("Acme.Export", "csv", null, TimeSpan.FromSeconds(0.5));

        // Assert
        IReadOnlyList<CollectedMeasurement<long>> jobs = jobCollector.GetMeasurementSnapshot();
        jobs.ShouldHaveSingleItem();
        jobs[0].Tags["status"].ShouldBe("Failed");
        jobs[0].Tags["tenant_id"].ShouldBe("global");

        IReadOnlyList<CollectedMeasurement<double>> durations = durationCollector.GetMeasurementSnapshot();
        durations.ShouldHaveSingleItem();
        durations[0].Value.ShouldBe(0.5, 0.01);
    }
}
