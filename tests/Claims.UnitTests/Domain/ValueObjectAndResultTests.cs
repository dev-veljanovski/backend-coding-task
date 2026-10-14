using Claims.Domain.Claims;
using Claims.Domain.Covers;
using Claims.Domain.Results;

namespace Claims.UnitTests.Domain;

public sealed class InsurancePeriodTests
{
    private static readonly DateOnly Start = new(2027, 3, 1);

    [Fact]
    public void DurationInDays_IsEndMinusStart()
    {
        InsurancePeriod.Create(Start, Start.AddDays(45)).Value.DurationInDays.ShouldBe(45);
    }

    [Fact]
    public void Create_OneYearInALeapYear_Has366Days()
    {
        var start = new DateOnly(2028, 1, 1);

        InsurancePeriod.Create(start, start.AddYears(1)).Value.DurationInDays.ShouldBe(366);
    }

    [Fact]
    public void Equality_IsByValue()
    {
        InsurancePeriod.Create(Start, Start.AddDays(5)).Value
            .ShouldBe(InsurancePeriod.Create(Start, Start.AddDays(5)).Value);
    }
}

public sealed class DamageCostTests
{
    [Fact]
    public void Create_WithinLimits_Succeeds()
    {
        DamageCost.Create(99_999.99m).Value.Amount.ShouldBe(99_999.99m);
    }

    [Fact]
    public void MaxAmount_Is100000()
    {
        DamageCost.MaxAmount.ShouldBe(100_000m);
    }
}

public sealed class ResultTests
{
    private static readonly Error SampleError = Error.Validation("Sample.Code", "Sample message", "field");

    [Fact]
    public void Value_OfFailedResult_Throws()
    {
        Result<int> result = SampleError;

        Should.Throw<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void ImplicitConversions_CreateSuccessAndFailure()
    {
        Result<int> success = 42;
        Result<int> failure = SampleError;

        success.IsSuccess.ShouldBeTrue();
        success.Value.ShouldBe(42);
        failure.IsFailure.ShouldBeTrue();
        failure.Errors.ShouldHaveSingleItem().ShouldBe(SampleError);
    }

    [Fact]
    public void Failure_WithoutErrors_IsNotAllowed()
    {
        Should.Throw<ArgumentException>(() => Result.Failure([]));
    }

    [Fact]
    public void Map_PropagatesFailures()
    {
        Result<int> failure = SampleError;

        failure.Map(value => value * 2).Errors.ShouldHaveSingleItem().ShouldBe(SampleError);
    }

    [Fact]
    public void Match_SelectsBranch()
    {
        Result<int> success = 1;
        Result<int> failure = SampleError;

        success.Match(_ => "ok", _ => "error").ShouldBe("ok");
        failure.Match(_ => "ok", _ => "error").ShouldBe("error");
    }
}
