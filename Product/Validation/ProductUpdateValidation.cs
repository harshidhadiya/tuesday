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
            RuleFor(x=>x.ids).Must(x=>x.Count()<=5).WithMessage("You can only update 5 images at a time").When(x=>x.ids!=null);
             RuleFor(x=>x.images).Must(x=>x.Count()<=5).WithMessage("You can only upload 5 images at a time").When(x=>x.images!=null);
             RuleFor(x=>x.ids).Must((productUpdate,ids)=> {
               return ids.Count()==productUpdate.images.Count();
             }).WithMessage("Your IDs count and product images count should be match right").When(x=>x.ids!=null && x.images!=null && x.ids.Count()>0 && x.images.Count()>0);
        }
    }
}