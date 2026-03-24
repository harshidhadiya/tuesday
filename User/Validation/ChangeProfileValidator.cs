using FluentValidation;
using USER.Data.Dto;

namespace USER.Validation
{
    public class ChangeProfileValidator : AbstractValidator<changeProfileDto>
    {
        public ChangeProfileValidator()
        {
            RuleFor(x => x.Name)
                .Matches("^[a-zA-Z ]+$")
                .When(x => !string.IsNullOrWhiteSpace(x.Name))
                .WithMessage("Name must contain only letters");

            RuleFor(x => x.Phone)
                .Matches(@"^[0-9]{10}$")
                .When(x => !string.IsNullOrWhiteSpace(x.Phone))
                .WithMessage("Phone must contain exactly 10 digits");

            RuleFor(x => x.Email)
                .EmailAddress()
                .When(x => !string.IsNullOrWhiteSpace(x.Email))
                .WithMessage("Invalid email format");

            RuleFor(x => x.Address)
                .MaximumLength(200)
                .When(x => !string.IsNullOrWhiteSpace(x.Address))
                .WithMessage("Address must be less than 200 characters");

          

            RuleFor(x => x.Password)
                .MinimumLength(8)
                .When(x => !string.IsNullOrWhiteSpace(x.Password))
                .WithMessage("Password must be at least 8 characters");

            RuleFor(x => x.confirmPassword)
                .Equal(x => x.Password)
                .When(x => !string.IsNullOrWhiteSpace(x.Password))
                .WithMessage("ConfirmPassword must match Password");
                RuleFor(x=>x.publicId).Must(X=>X==null).WithMessage("Sorry But You are Not Authorize To Set This Okay");
        }
    }
}