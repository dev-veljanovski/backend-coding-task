namespace Claims.Domain.Covers.Pricing;

public interface IPremiumCalculator
{
    decimal Calculate(CoverType coverType, InsurancePeriod period);
}
