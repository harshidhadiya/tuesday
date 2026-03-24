    using ADMIN.Data.Dto;
    using FluentValidation;
namespace ADMIN.Validation
{

public class FilterValidator : AbstractValidator<Filter>
{
    public FilterValidator()
    {
        RuleFor(x => x.name)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.name));

        RuleFor(x => x.email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.email));

        RuleFor(x => x)
            .Must(x => x.From == null || x.To == null || x.From <= x.To)
            .WithMessage("From date must be less than or equal to To date");

        RuleFor(x => x.page)
            .GreaterThan(0)
            .WithMessage("Page must be greater than 0");

        RuleFor(x => x.pageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100)
            .WithMessage("PageSize must be between 1 and 100");

        RuleFor(x => x.From)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .When(x => x.From.HasValue);

        RuleFor(x => x.To)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .When(x => x.To.HasValue);
    }
}
}