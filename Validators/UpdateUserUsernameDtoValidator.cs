using BlogGraphQlApp.DTOs;
using FluentValidation;

namespace BlogGraphQlApp.Validators
{
    public class UpdateUserUsernameDtoValidator : AbstractValidator<UpdateUserUsernameDto>
    {
        public UpdateUserUsernameDtoValidator()
        {
            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Username is required.")
                .Matches("^[a-zA-Z_]+$").WithMessage("Username can only contain letters and underscores.");
        }
    }
}