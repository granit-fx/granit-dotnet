using Granit.Identity.Local.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// Maps an unsuccessful <see cref="IdentityResult"/> to a classified
/// <see cref="IdentityOperationException"/>, translating ASP.NET Identity error
/// <em>codes</em> (stable identifiers) into <see cref="IdentityOperationErrorKind"/>.
/// </summary>
/// <remarks>
/// Classification keys on <see cref="IdentityError.Code"/>, never
/// <see cref="IdentityError.Description"/>: descriptions are localized by the configured
/// <see cref="IdentityErrorDescriber"/> and must not drive control flow.
/// </remarks>
internal static class IdentityResultClassifier
{
    public static IdentityOperationException ToOperationException(this IdentityResult result, string operation)
    {
        IReadOnlyList<IdentityOperationError> errors = result.Errors
            .Select(e => new IdentityOperationError(e.Code ?? string.Empty, Classify(e.Code), e.Description ?? string.Empty))
            .ToList();

        return new IdentityOperationException(operation, errors);
    }

    private static IdentityOperationErrorKind Classify(string? code) => code switch
    {
        "DuplicateUserName" => IdentityOperationErrorKind.DuplicateUserName,
        "DuplicateEmail" => IdentityOperationErrorKind.DuplicateEmail,
        "InvalidEmail" => IdentityOperationErrorKind.InvalidEmail,
        "InvalidUserName" => IdentityOperationErrorKind.InvalidUserName,
        not null when code.StartsWith("Password", StringComparison.Ordinal) => IdentityOperationErrorKind.PasswordPolicy,
        _ => IdentityOperationErrorKind.Unknown,
    };
}
