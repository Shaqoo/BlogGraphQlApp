using BlogGraphQlApp.DTOs;
using FluentValidation;

namespace BlogGraphQlApp.Validators
{
    public class UpdateUserBackgroundIdentifierDtoValidator : AbstractValidator<UpdateUserBackgroundIdentifierDto>
    {
        public UpdateUserBackgroundIdentifierDtoValidator()
        {
            RuleFor(x => x.BackgroundIdentifier)
                .NotEmpty().WithMessage("Background identifier cannot be empty.").When(x => x.BackgroundIdentifier != null);
        }
    }
}