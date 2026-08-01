using BlogGraphQlApp.DTOs;
using FluentValidation;

namespace BlogGraphQlApp.Validators
{
    public class SearchGroupMessagesValidator : AbstractValidator<GroupMessageSearchInput>
    {
        public SearchGroupMessagesValidator()
        {
            RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be at least 1.");
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
        }
    }
}
