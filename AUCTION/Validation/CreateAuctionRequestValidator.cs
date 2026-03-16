using AUCTION.Data.Dto.Request;
using FluentValidation;
namespace AUCTION.Validation
{
   
public class CreateAuctionRequestValidator : AbstractValidator<CreateAuctionRequest>
{
    public CreateAuctionRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0)
            .WithMessage("ProductId must be greater than 0.");

        RuleFor(x => x.StartingPrice)
            .GreaterThan(0)
            .WithMessage("Starting price must be greater than 0.");

        RuleFor(x => x.ReservePrice)
            .GreaterThan(x => x.StartingPrice)
            .When(x => x.ReservePrice.HasValue)
            .WithMessage("Reserve price must be greater than starting price.");

        RuleFor(x => x.MinBidIncrement)
            .GreaterThan(0)
            .WithMessage("Minimum bid increment must be greater than 0.");

        RuleFor(x => x.StartDate)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Start date must be in the future.");

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be greater than start date.");

        RuleFor(x => x)
            .Must(x => (x.EndDate - x.StartDate).TotalMinutes >= 1)
            .WithMessage("Auction duration must be at least 1 minute.");
        RuleFor(x=>x).Must(x=>(x.EndDate-x.StartDate).TotalMinutes <=35).WithMessage("Sorry But duration of the your auction doesnot exeece from the 35 minitues");
    }
} 
}