using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Encryption.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the encryption module.
/// Meter: <c>Granit.Encryption</c>.
/// </summary>
public sealed class EncryptionMetrics
{
    public const string MeterName = "Granit.Encryption";

    private readonly Counter<long> _keysCreated;
    private readonly Counter<long> _keysShredded;
    private readonly Counter<long> _shreddingErrors;

    public EncryptionMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _keysCreated = meter.CreateCounter<long>(
            "granit.encryption.key.created",
            description: "Number of per-entity encryption keys created.");

        _keysShredded = meter.CreateCounter<long>(
            "granit.encryption.key.shredded",
            description: "Number of per-entity encryption keys permanently destroyed (crypto-shredding).");

        _shreddingErrors = meter.CreateCounter<long>(
            "granit.encryption.shredding.errors",
            description: "Number of crypto-shredding operations that failed.");
    }

    public void RecordKeyCreated(string? tenantId, string entityType) =>
        _keysCreated.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "entity_type", entityType },
        });

    public void RecordKeyShredded(string? tenantId, string entityType) =>
        _keysShredded.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "entity_type", entityType },
        });

    public void RecordShreddingError(string? tenantId, string entityType) =>
        _shreddingErrors.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "entity_type", entityType },
        });
}
