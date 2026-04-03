using Granit.Domain;

namespace Granit.Invoicing.Domain;

/// <summary>A generated document (PDF) attached to an invoice.</summary>
public sealed class InvoiceDocument : Entity
{
    private InvoiceDocument() { }

    public static InvoiceDocument Create(
        Guid id, Guid blobId, string fileName, string contentType, DateTimeOffset generatedAt) =>
        new()
        {
            Id = id,
            BlobId = blobId,
            FileName = fileName,
            ContentType = contentType,
            GeneratedAt = generatedAt,
        };

    public Guid BlobId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; private set; }
}
