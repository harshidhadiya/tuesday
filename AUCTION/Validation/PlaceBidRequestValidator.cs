using AUCTION.Data.Dto.Request;
using FluentValidation;

namespace AUCTION.Validation
{
    public class PlaceBidRequestValidator :AbstractValidator<PlaceBidRequest>
    {
      public PlaceBidRequestValidator()
        {
            RuleFor(x=>x.Amount).GreaterThan(0).WithMessage("With lesser then 0 amount is not acceptable for the bid");
        }   
    }
}