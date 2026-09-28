using FluentValidation;

using MediatR;

namespace TestDoubles.Helpers;

public sealed record ValidationTestRequest(string Username, string Email);

public sealed record ValidationTestResponse(string Value);

public static class ValidationTestFixtures
{
    public static readonly RequestHandlerDelegate<ValidationTestResponse> OkDelegate =
        _ => Task.FromResult(new ValidationTestResponse("ok"));

    public sealed class ValidationTestValidator : AbstractValidator<ValidationTestRequest>
    {
        public ValidationTestValidator()
        {
            RuleFor(x => x.Username).NotEmpty().WithMessage("Username is required.");
            RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required.");
        }
    }
}
