using FluentValidation;
using VERIFY.DTOs.Requests;

public class CreateAuctionRequestValidator : AbstractValidator<CreateAuctionRequest>
{
    public CreateAuctionRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0).WithMessage("ProductId must be valid.");

        RuleFor(x => x.StartingPrice)
            .GreaterThan(0).WithMessage("Starting price must be greater than 0.");

        RuleFor(x => x.ReservePrice)
            .GreaterThanOrEqualTo(x => x.StartingPrice)
            .When(x => x.ReservePrice.HasValue)
            .WithMessage("Reserve price must be greater than or equal to starting price.");

        RuleFor(x => x.MinBidIncrement)
            .GreaterThan(0).WithMessage("Minimum bid increment must be greater than 0.");

        RuleFor(x => x.StartDate)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Start date must be in the future.");

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be greater than start date.");

        RuleFor(x => x)
            .Must(x => (x.EndDate - x.StartDate).TotalMinutes <= 30)
            .WithMessage("Auction duration cannot exceed 30 minutes.");
    }
}