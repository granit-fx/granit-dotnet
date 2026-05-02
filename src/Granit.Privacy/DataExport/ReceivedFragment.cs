using Granit.Domain.ValueObjects;

namespace Granit.Privacy.DataExport;

/// <summary>
/// A fragment received from a data provider during the privacy export Saga.
/// </summary>
public sealed record ReceivedFragment(string ProviderName, BlobReference BlobReferenceId, string ContentType);
