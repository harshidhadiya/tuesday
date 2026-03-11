using FluentValidation;
using PRODUCT.Data.Dto.Request;

namespace PRODUCT.Validation
{
    public class ProductUpdateValidation : AbstractValidator<ProductUpdate>
    {
        public ProductUpdateValidation()
        {
            RuleFor(x => x.name)
                .NotEmpty()
                .WithMessage("Product name cannot be empty.")
                .When(x => x.name != null);

            RuleFor(x => x.description)
                .NotEmpty()
                .WithMessage("Product description cannot be empty.")
                .When(x => x.description != null);

            RuleFor(x => x.date)
                .NotEmpty()
                .WithMessage("Product date cannot be empty.")
                .When(x => x.date != null);

            RuleFor(x => x.AuctionStartTime)
                .GreaterThan(DateTime.UtcNow)
                .WithMessage("Auction start time must be greater than current time.")
                .When(x => x.AuctionStartTime != null);

            RuleFor(x => x.AuctionEndTime)
                .GreaterThan(x => x.AuctionStartTime)
                .WithMessage("Auction end time must be greater than auction start time.")
                .When(x => x.AuctionEndTime != null && x.AuctionStartTime != null);
        }
    }
}