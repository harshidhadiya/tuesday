
using System.Data;
using FluentValidation;
using VERIFY.DTOs.Requests;

namespace VERIFY.Validation
{
    public class verifyProductValidator : AbstractValidator<VerifyProductRequest>
    {
        public verifyProductValidator()
        {
            RuleFor(x=>x.ProductId).Must(x=>x>0).WithMessage("Request should be contain valid Product Id");
            RuleFor(x=>x.ProductName).NotEmpty().WithMessage("Your product name shouldm't be empty");
            RuleFor(x=>x.SellerId).Must(x=>x>0).WithMessage("Enter the Seller Id");
        }
    }
}