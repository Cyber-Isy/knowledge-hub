using FluentValidation;
using KnowledgeHub.API.DTOs;

namespace KnowledgeHub.API.Validators;

public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message content is required.")
            .MaximumLength(4000).WithMessage("Message content must not exceed 4000 characters.");
    }
}
