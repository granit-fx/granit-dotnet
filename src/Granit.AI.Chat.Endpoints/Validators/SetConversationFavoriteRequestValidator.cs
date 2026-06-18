using Granit.AI.Chat.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.AI.Chat.Endpoints.Validators;

/// <summary>
/// Validates <see cref="SetConversationFavoriteRequest"/>. The single <c>bool</c> field is
/// unconstrained (both states are valid), so the validator carries no rules; it exists to satisfy
/// the framework's "every <c>*Request</c> has a <see cref="GranitValidator{T}"/>" convention.
/// </summary>
internal sealed class SetConversationFavoriteRequestValidator : GranitValidator<SetConversationFavoriteRequest>
{
}
