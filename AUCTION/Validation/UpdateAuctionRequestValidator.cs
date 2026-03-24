using AUCTION.Data.Dto.Request;
using AUCTION.Data.Entities;
using FluentValidation;

namespace AUCTION.Validation
{
    public class UpdateAuctionRequestValidator : AbstractValidator<UpdateAuctionRequest>
    {
        public UpdateAuctionRequestValidator()
        {
              
            RuleFor(x => x.StartingPrice)
                .GreaterThan(0)
                .When(x => x.StartingPrice.HasValue)
                .WithMessage("Starting price must be greater than 0.");

            RuleFor(x => x.ReservePrice)
                .GreaterThan(0)
                .When(x => x.ReservePrice.HasValue)
                .WithMessage("Reserve price must be greater than 0.");

            RuleFor(x => x.MinBidIncrement)
                .GreaterThan(0)
                .When(x => x.MinBidIncrement.HasValue)
                .WithMessage("Minimum bid increment must be greater than 0.");

            RuleFor(x => x.StartDate)
                .GreaterThan(TimeHelper.Now())
                .When(x => x.StartDate.HasValue)
                .WithMessage("Start date must be in the future.");

            RuleFor(x => x.EndDate)
                .Must((model, endDate) =>
                {
                    if (model.StartDate is null || endDate is null)
                        return true;

                    return endDate.Value > model.StartDate.Value;
                })
                .WithMessage("End date must be greater than start date.");

            RuleFor(x => x)
                .Must(x =>
                {
                    if (x.StartDate is null || x.EndDate is null)
                        return true;

                    return (x.EndDate.Value - x.StartDate.Value).TotalMinutes <= 35;
                })
                .WithMessage("Auction duration cannot exceed 35 minutes.");
        }
    }
}