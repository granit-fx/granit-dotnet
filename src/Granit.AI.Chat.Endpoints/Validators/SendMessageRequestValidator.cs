using FluentValidation;
using Granit.AI.Chat.Endpoints.Dtos;
using Granit.AI.Chat.Options;
using Granit.Validation;
using Granit.Validation.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.AI.Chat.Endpoints.Validators;

/// <summary>Validates <see cref="SendMessageRequest"/>.</summary>
internal sealed class SendMessageRequestValidator : GranitValidator<SendMessageRequest>
{
    /// <summary>Maximum message length.</summary>
    public const int MaxMessageLength = 16000;

    /// <summary>Maximum number of <c>@</c> mentions on a single turn.</summary>
    public const int MaxMentions = 25;

    /// <summary>Maximum number of <c>/</c> prompt badges on a single turn.</summary>
    public const int MaxPromptRefs = 5;

    public SendMessageRequestValidator(IOptions<GranitAIChatAttachmentOptions> attachmentOptions)
    {
        GranitAIChatAttachmentOptions limits = attachmentOptions.Value;

        RuleFor(x => x.Message).NotEmpty().MaximumLength(MaxMessageLength);

        RuleFor(x => x.Mentions)
            .Must(m => m is null || m.Count <= MaxMentions)
            .WithErrorCodeAndMessage("AIChat:Validation:TooManyMentions");

        RuleForEach(x => x.Mentions)
            .ChildRules(mention =>
            {
                mention.RuleFor(m => m.Type).NotEmpty();
                mention.RuleFor(m => m.Id).NotEmpty();
            })
            .When(x => x.Mentions is { Count: > 0 });

        RuleFor(x => x.PromptRefs)
            .Must(p => p is null || p.Count <= MaxPromptRefs)
            .WithErrorCodeAndMessage("AIChat:Validation:TooManyPromptRefs");

        RuleFor(x => x.Attachments)
            .Must(a => a is null || a.Count <= limits.MaxAttachments)
            .WithErrorCodeAndMessage("AIChat:Validation:TooManyAttachments");

        RuleForEach(x => x.Attachments)
            .ChildRules(attachment =>
            {
                attachment.RuleFor(a => a.Reference).NotEmpty();
                attachment.RuleFor(a => a.FileName).NotEmpty();
                attachment.RuleFor(a => a.ContentType)
                    .Must(limits.AllowedContentTypes.Contains)
                    .WithErrorCodeAndMessage("AIChat:Validation:UnsupportedAttachmentType");
                attachment.RuleFor(a => a.SizeBytes)
                    .InclusiveBetween(1, limits.MaxAttachmentBytes)
                    .WithErrorCodeAndMessage("AIChat:Validation:AttachmentTooLarge");
            })
            .When(x => x.Attachments is { Count: > 0 });
    }
}
