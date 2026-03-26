using FluentValidation;
using VERIFY.DTOs.Requests;

namespace VERIFY.Validators
{
    public class ProductUnverifyValidator : AbstractValidator<ProductUnverify>
    {
        public ProductUnverifyValidator()
        {
            
            RuleFor(x => x.productId)
                .GreaterThan(0)
                .WithMessage("ProductId must be greater than 0.");

            RuleFor(x => x.description)
                .MaximumLength(500)
                .WithMessage("Description cannot exceed 500 characters.");
        }
    }
}