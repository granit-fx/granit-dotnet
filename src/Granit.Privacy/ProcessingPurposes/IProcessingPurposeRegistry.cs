namespace Granit.Privacy.ProcessingPurposes;

/// <summary>
/// Singleton registry of declared processing purposes, populated at startup.
/// </summary>
public interface IProcessingPurposeRegistry
{
    /// <summary>Returns all registered processing purpose definitions.</summary>
    IReadOnlyList<ProcessingPurposeDefinition> GetAll();

    /// <summary>Returns the purpose with the specified ID, or <c>null</c> if not found.</summary>
    ProcessingPurposeDefinition? GetPurpose(string purposeId);

    /// <summary>Returns all purposes declared with the specified legal basis.</summary>
    IReadOnlyList<ProcessingPurposeDefinition> GetByLegalBasis(string legalBasis);
}
