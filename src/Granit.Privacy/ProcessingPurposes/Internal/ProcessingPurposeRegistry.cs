using System.Collections.Frozen;

namespace Granit.Privacy.ProcessingPurposes.Internal;

/// <summary>
/// Frozen dictionary-backed registry of processing purposes, built at startup.
/// </summary>
internal sealed class ProcessingPurposeRegistry : IProcessingPurposeRegistry
{
    private readonly FrozenDictionary<string, ProcessingPurposeDefinition> _purposes;
    private readonly IReadOnlyList<ProcessingPurposeDefinition> _all;

    internal ProcessingPurposeRegistry(IEnumerable<ProcessingPurposeDefinition> definitions)
    {
        _purposes = definitions.ToFrozenDictionary(d => d.PurposeId, StringComparer.OrdinalIgnoreCase);
        _all = [.. _purposes.Values];
    }

    public IReadOnlyList<ProcessingPurposeDefinition> GetAll() => _all;

    public ProcessingPurposeDefinition? GetPurpose(string purposeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purposeId);
        return _purposes.TryGetValue(purposeId, out ProcessingPurposeDefinition? definition) ? definition : null;
    }

    public IReadOnlyList<ProcessingPurposeDefinition> GetByLegalBasis(string legalBasis)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legalBasis);
        return _all.Where(p => string.Equals(p.LegalBasis, legalBasis, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
