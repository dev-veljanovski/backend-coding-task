using Claims.Domain.Claims;
using Claims.Domain.Claims.Events;
using Claims.UnitTests.TestDoubles;

namespace Claims.UnitTests.Domain;

// Task 2: claim rules.
public sealed class ClaimTests
{
    private static readonly DateOnly Start = TestData.Today.AddDays(10);
    private static readonly DateOnly End = Start.AddDays(90);

    [Fact]
    public void File_WithValidInput_CreatesClaimLinkedToCover()
    {
        var cover = TestData.NewCover(Start, End);

        var claim = Claim.File(cover, "  Hull damage  ", ClaimType.Collision, 25_000m, Start.AddDays(5));

        claim.IsSuccess.ShouldBeTrue();
        claim.Value.CoverId.ShouldBe(cover.Id);
        claim.Value.Name.ShouldBe("Hull damage");
        claim.Value.Type.ShouldBe(ClaimType.Collision);
        claim.Value.DamageCost.Amount.ShouldBe(25_000m);
        claim.Value.Created.ShouldBe(Start.AddDays(5));
    }

    [Fact]
    public void File_RaisesClaimCreatedEvent()
    {
        var cover = TestData.NewCover(Start, End);

        var claim = Claim.File(cover, "Fire in engine room", ClaimType.Fire, 1_000m, Start).Value;

        var domainEvent = claim.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ClaimCreatedDomainEvent>();
        domainEvent.ClaimId.ShouldBe(claim.Id.Value);
        domainEvent.CoverId.ShouldBe(cover.Id.Value);
    }

    [Fact]
    public void File_DamageCostOfExactly100000_IsAllowed()
    {
        var cover = TestData.NewCover(Start, End);

        Claim.File(cover, "Max", ClaimType.Grounding, 100_000m, Start).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("100000.01")]
    [InlineData("250000")]
    public void File_DamageCostAbove100000_IsRejected(string damageCost)
    {
        var cover = TestData.NewCover(Start, End);

        var claim = Claim.File(cover, "Too expensive", ClaimType.Grounding, decimal.Parse(damageCost, System.Globalization.CultureInfo.InvariantCulture), Start);

        var error = claim.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("Claim.DamageCostExceedsLimit");
        error.Target.ShouldBe("damageCost");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void File_DamageCostNotPositive_IsRejected(string damageCost)
    {
        var cover = TestData.NewCover(Start, End);

        var claim = Claim.File(cover, "Free", ClaimType.Grounding, decimal.Parse(damageCost, System.Globalization.CultureInfo.InvariantCulture), Start);

        claim.Errors.ShouldHaveSingleItem().Code.ShouldBe("Claim.DamageCostNotPositive");
    }

    [Fact]
    public void File_OnFirstAndLastDayOfCover_IsAllowed()
    {
        var cover = TestData.NewCover(Start, End);

        Claim.File(cover, "First day", ClaimType.BadWeather, 10m, Start).IsSuccess.ShouldBeTrue();
        Claim.File(cover, "Last day", ClaimType.BadWeather, 10m, End).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(91)]
    public void File_CreatedOutsideCoverPeriod_IsRejected(int offsetFromStart)
    {
        var cover = TestData.NewCover(Start, End);

        var claim = Claim.File(cover, "Outside", ClaimType.BadWeather, 10m, Start.AddDays(offsetFromStart));

        var error = claim.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("Claim.CreatedOutsideCoverPeriod");
        error.Target.ShouldBe("created");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void File_WithoutName_IsRejected(string name)
    {
        var cover = TestData.NewCover(Start, End);

        Claim.File(cover, name, ClaimType.Fire, 10m, Start).Errors.ShouldHaveSingleItem().Code.ShouldBe("Claim.NameRequired");
    }

    [Fact]
    public void File_WithSeveralViolations_ReportsAllOfThem()
    {
        var cover = TestData.NewCover(Start, End);

        var claim = Claim.File(cover, "", ClaimType.Fire, 500_000m, End.AddDays(1));

        claim.Errors.Select(e => e.Code).ShouldBe(
            ["Claim.NameRequired", "Claim.DamageCostExceedsLimit", "Claim.CreatedOutsideCoverPeriod"],
            ignoreOrder: true);
    }

    [Fact]
    public void Delete_RaisesClaimDeletedEvent()
    {
        var claim = Claim.File(TestData.NewCover(Start, End), "To delete", ClaimType.Fire, 10m, Start).Value;
        claim.ClearDomainEvents();

        claim.Delete();

        claim.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ClaimDeletedDomainEvent>().ClaimId.ShouldBe(claim.Id.Value);
    }
}
