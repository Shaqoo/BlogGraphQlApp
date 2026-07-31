using System.ComponentModel.DataAnnotations;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using FluentValidation;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class AuthMutation
    {
        public record LoginInput(
            [Required, EmailAddress] string Email,
            [Required] string Password);

        public record CreateUserInput(
            [Required, RegularExpression("^[a-zA-Z_]+$")] string Username,
            [Required, EmailAddress] string Email,
            [Required, RegularExpression("^[a-zA-Z ]+$")] string FullName,
            [Required, MinLength(8), RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).*$", ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.")] string Password,
            [Required] string ConfirmPassword);

        public record RequestVerificationCodeInput([Required, EmailAddress] string Email);

        public record VerifyEmailInput(
            [Required, EmailAddress] string Email,
            [Required] string Code);

        public record ForgotPasswordInput([Required, EmailAddress] string Email);

        public record ResetPasswordInput(
            [Required, EmailAddress] string Email,
            [Required] string Token,
            [Required, MinLength(8), RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).*$", ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.")] string NewPassword,
            [Required] string ConfirmPassword);

     

        public async Task<ApiResponse<string>> LoginAsync(
            LoginInput input,
            [Service] IAuthService authService)
        {
            return await authService.LoginAsync(input.Email, input.Password);
        }

        [GraphQLDescription("Registers a new user.")]
        public async Task<ApiResponse<UserDto>> CreateUserAsync(
            CreateUserInput input,
            [Service] IUserService userService,
            [Service] IValidator<CreateUserDto> validator)
        {
            var createUserDto = new CreateUserDto(input.Username, input.Email, input.FullName, input.Password, input.ConfirmPassword);
            var validationResult = await validator.ValidateAsync(createUserDto);

            if (!validationResult.IsValid)
            {
                return ApiResponse<UserDto>.Fail("Validation failed.", validationResult.Errors.Select(e => e.ErrorMessage).ToList());
            }

            return await userService.CreateUserAsync(createUserDto);
        }

        public async Task<ApiResponse<bool>> RequestVerificationCodeAsync(
            RequestVerificationCodeInput input,
            [Service] IAuthService authService)
        {
            return await authService.RequestVerificationCodeAsync(input.Email);
        }

        public async Task<ApiResponse<string>> VerifyEmailAsync(
            VerifyEmailInput input,
            [Service] IAuthService authService)
        {
            return await authService.VerifyEmailAsync(input.Email, input.Code);
        }

        public async Task<ApiResponse<bool>> ForgotPasswordAsync(
            ForgotPasswordInput input,
            [Service] IAuthService authService)
        {
            return await authService.ForgotPasswordAsync(input.Email);
        }

        public async Task<ApiResponse<bool>> ResetPasswordAsync(
            ResetPasswordInput input,
            [Service] IAuthService authService,
            [Service] IValidator<ResetPasswordInput> validator)
        {
            var validationResult = await validator.ValidateAsync(input);
            if (!validationResult.IsValid)
            {
                return ApiResponse<bool>.Fail("Validation failed.", validationResult.Errors.Select(e => e.ErrorMessage).ToList());
            }

            return await authService.ResetPasswordAsync(input.Email, input.Token, input.NewPassword, input.ConfirmPassword);
        }
    }
}