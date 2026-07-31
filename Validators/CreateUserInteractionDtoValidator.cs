using BlogGraphQlApp.DTOs;
using FluentValidation;

namespace BlogGraphQlApp.Validators
{
    public class CreateUserInteractionDtoValidator : AbstractValidator<CreateUserInteractionDto>
    {
        public CreateUserInteractionDtoValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.PostId).NotNull().When(x => x.ReelId == null)
                .WithMessage("Either PostId or ReelId must be provided.");
            RuleFor(x => x.ReelId).NotNull().When(x => x.PostId == null)
                .WithMessage("Either PostId or ReelId must be provided.");
            RuleFor(x => x.TimeSpentInSeconds).GreaterThanOrEqualTo(0);
        }
    }
}