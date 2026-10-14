using Claims.Domain.Covers;
using Claims.Domain.Covers.Events;
using Claims.UnitTests.TestDoubles;

namespace Claims.UnitTests.Domain;

// Task 2: cover rules.
public sealed class CoverTests
{
    private static readonly DateOnly Today = TestData.Today;

    [Fact]
    public void Issue_WithValidInput_CreatesCoverWithComputedPremium()
    {
        var cover = Cover.Issue(Today, Today.AddDays(30), CoverType.Yacht, Today, TestData.PremiumCalculator);

        cover.IsSuccess.ShouldBeTrue();
        cover.Value.Premium.ShouldBe(41250m);
        cover.Value.Period.StartDate.ShouldBe(Today);
        cover.Value.Period.EndDate.ShouldBe(Today.AddDays(30));
        cover.Value.Type.ShouldBe(CoverType.Yacht);
    }

    [Fact]
    public void Issue_RaisesCoverCreatedEvent()
    {
        var cover = TestData.NewCover();

        var domainEvent = cover.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<CoverCreatedDomainEvent>();
        domainEvent.CoverId.ShouldBe(cover.Id.Value);
    }

    [Fact]
    public void Issue_StartingToday_IsAllowed()
    {
        Cover.Issue(Today, Today.AddDays(1), CoverType.Tanker, Today, TestData.PremiumCalculator).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Issue_StartDateInThePast_IsRejected()
    {
        var cover = Cover.Issue(Today.AddDays(-1), Today.AddDays(10), CoverType.Yacht, Today, TestData.PremiumCalculator);

        cover.IsFailure.ShouldBeTrue();
        cover.Errors.ShouldHaveSingleItem().Code.ShouldBe("Cover.StartDateInPast");
    }

    [Fact]
    public void Issue_PeriodOfExactlyOneYear_IsAllowed()
    {
        var start = Today.AddDays(1);

        Cover.Issue(start, start.AddYears(1), CoverType.Yacht, Today, TestData.PremiumCalculator).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Issue_PeriodLongerThanOneYear_IsRejected()
    {
        var start = Today.AddDays(1);

        var cover = Cover.Issue(start, start.AddYears(1).AddDays(1), CoverType.Yacht, Today, TestData.PremiumCalculator);

        cover.Errors.ShouldHaveSingleItem().Code.ShouldBe("Cover.PeriodExceedsOneYear");
    }

    [Fact]
    public void Issue_EndDateNotAfterStartDate_IsRejected()
    {
        var cover = Cover.Issue(Today, Today, CoverType.Yacht, Today, TestData.PremiumCalculator);

        cover.Errors.ShouldHaveSingleItem().Code.ShouldBe("Cover.EndDateNotAfterStartDate");
    }

    [Fact]
    public void Issue_WithSeveralViolations_ReportsAllOfThem()
    {
        var start = Today.AddDays(-5);

        var cover = Cover.Issue(start, start.AddYears(2), CoverType.Yacht, Today, TestData.PremiumCalculator);

        cover.Errors.Select(e => e.Code).ShouldBe(["Cover.StartDateInPast", "Cover.PeriodExceedsOneYear"], ignoreOrder: true);
    }

    [Fact]
    public void Delete_RaisesCoverDeletedEvent()
    {
        var cover = TestData.NewCover();
        cover.ClearDomainEvents();

        cover.Delete();

        cover.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<CoverDeletedDomainEvent>().CoverId.ShouldBe(cover.Id.Value);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(15, true)]
    [InlineData(30, true)]
    [InlineData(-1, false)]
    [InlineData(31, false)]
    public void IsActiveOn_IncludesBothBounds(int offsetFromStart, bool expected)
    {
        var cover = TestData.NewCover(Today, Today.AddDays(30));

        cover.IsActiveOn(Today.AddDays(offsetFromStart)).ShouldBe(expected);
    }
}
