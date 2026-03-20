namespace Granit.Validation.ServerValidation;

/// <summary>
/// Contributes <see cref="IServerValidator"/> instances to the
/// <see cref="ServerValidatorRegistry"/>.
/// </summary>
/// <remarks>
/// Implement this interface in each validation package (core, regional, or application)
/// to register validators in bulk. Implementations are auto-discovered from all loaded
/// module assemblies by <c>GranitValidationModule</c>.
/// </remarks>
public interface IServerValidatorContributor
{
    /// <summary>
    /// Returns the validators contributed by this package or module.
    /// </summary>
    IEnumerable<IServerValidator> GetValidators();
}
