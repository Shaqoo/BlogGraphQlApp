using BlogGraphQlApp.DTOs;
using FluentValidation;

namespace BlogGraphQlApp.Validators
{
    public class UpdateUserBioDtoValidator : AbstractValidator<UpdateUserBioDto>
    {
        public UpdateUserBioDtoValidator()
        {
            RuleFor(x => x.Bio)
                .MaximumLength(500).WithMessage("Bio cannot be longer than 500 characters.");
        }
    }
}