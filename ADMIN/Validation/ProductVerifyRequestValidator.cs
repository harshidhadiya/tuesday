    using ADMIN.DTOs.Requests;
    using FluentValidation;
namespace ADMIN.Validation
{

    public class ProductVerifyRequestValidator : AbstractValidator<ProductVerifyRequest>
    {
        public ProductVerifyRequestValidator()
        {
            RuleFor(x => x.ProductId)
                .GreaterThan(0).WithMessage("ProductId must be greater than 0.");

            RuleFor(x => x.Description)
                .NotNull().WithMessage("Description must be supplied.")
                .NotEmpty().WithMessage("Description must not be empty.")
                .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");
        }
    }
}
