using System.Collections.Frozen;
using Microsoft.Extensions.Logging;

namespace Granit.Validation.ServerValidation;

/// <summary>
/// Singleton registry that maps error codes to <see cref="IServerValidator"/> instances.
/// Built once at application startup from all <see cref="IServerValidatorContributor"/> implementations.
/// </summary>
public sealed class ServerValidatorRegistry
{
    private readonly FrozenDictionary<string, IServerValidator> _validators;

    /// <summary>
    /// Initializes the registry by collecting validators from all contributors.
    /// </summary>
    public ServerValidatorRegistry(
        IEnumerable<IServerValidatorContributor> contributors,
        ILogger<ServerValidatorRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        Dictionary<string, IServerValidator> dict = new(StringComparer.Ordinal);

        IEnumerable<(IServerValidatorContributor Contributor, IServerValidator Validator)> duplicates = contributors
            .SelectMany(c => c.GetValidators().Select(v => (Contributor: c, Validator: v)))
            .Where(x => !dict.TryAdd(x.Validator.ErrorCode, x.Validator));

        foreach ((IServerValidatorContributor contributor, IServerValidator validator) in duplicates)
        {
            logger.LogWarning(
                "Duplicate server validator for error code '{ErrorCode}' from {ContributorType}. Keeping the first registration.",
                validator.ErrorCode,
                contributor.GetType().Name);
        }

        _validators = dict.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// Returns the validator for the specified error code, or <see langword="null"/> if not registered.
    /// </summary>
    public IServerValidator? GetOrNull(string errorCode) =>
        _validators.GetValueOrDefault(errorCode);

    /// <summary>
    /// Returns all registered error codes. Useful for discovery endpoints.
    /// </summary>
    public IReadOnlyCollection<string> GetAllErrorCodes() => _validators.Keys;
}
