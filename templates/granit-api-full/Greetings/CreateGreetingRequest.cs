using FluentValidation;

namespace GranitApiFull.Greetings;

/// <summary>Request payload for creating a greeting.</summary>
/// <param name="Name">The name of the person to greet.</param>
public sealed record CreateGreetingRequest(string Name);

/// <summary>Validator for <see cref="CreateGreetingRequest"/>.</summary>
public sealed class CreateGreetingRequestValidator : AbstractValidator<CreateGreetingRequest>
{
    /// <summary>Initializes the validator.</summary>
    public CreateGreetingRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}
