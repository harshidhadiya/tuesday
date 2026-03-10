using FluentValidation;
using PRODUCT.Data.Dto;

namespace PRODUCT.Validation
{
    public class productCreateValidation : AbstractValidator<createProduct>
    {
        public productCreateValidation()
        {
            RuleFor(x => x.product_name).NotEmpty().WithMessage("Product name is required.");
            RuleFor(x => x.Buy_Date).NotEmpty().WithMessage("Buy date is required.");
            RuleFor(x => x.product_name).Matches(@"^[A-Za-z0-9\s]+$").WithMessage("Name Only Contains the number and alphabet letters");
            RuleFor(x => x.Buy_Date).Must(date => date <= DateTime.Now).WithMessage("Date must be in the past.");

        }
    }
}