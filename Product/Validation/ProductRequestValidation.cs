using FluentValidation;
using PRODUCT.Data.Dto.Request;

namespace PRODUCT.Validation
{
    public class ProductRequestValidation : AbstractValidator<ProductCreate>
    {
        public ProductRequestValidation()
        {
              RuleFor(x => x.name).NotEmpty().WithMessage("Product name is required.");
            RuleFor(x => x.date).NotEmpty().WithMessage("Buy date is required.");
            RuleFor(x => x.name).Matches(@"^[A-Za-z0-9\s]+$").WithMessage("Name Only Contains the number and alphabet letters");
            RuleFor(x => x.date).Must(date => date <= DateTime.Now).WithMessage("Date must be in the past.");
        }
    }
}