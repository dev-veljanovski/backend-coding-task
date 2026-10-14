using Claims.Application.Covers.CreateCover;
using Claims.Application.Covers.DeleteCover;
using Claims.Application.Covers.QuotePremium;
using Claims.Domain.Covers;
using Claims.Domain.Covers.Events;
using Claims.UnitTests.TestDoubles;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Claims.UnitTests.Application;

public sealed class CreateCoverCommandHandlerTests
{
    private readonly ICoverRepository _covers = Substitute.For<ICoverRepository>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 23, 30, 0, TimeSpan.Zero));
    private readonly CreateCoverCommandHandler _handler;

    public CreateCoverCommandHandlerTests() =>
        _handler = new CreateCoverCommandHandler(_covers, TestData.PremiumCalculator, _time);

    [Fact]
    public async Task Handle_ValidCover_ComputesPremiumAndRegistersCover()
    {
        var start = new DateOnly(2026, 10, 15);
        var command = new CreateCoverCommand(start, start.AddDays(30), CoverType.Yacht);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.Value.Premium.ShouldBe(41_250m);
        _covers.Received(1).Add(Arg.Is<Cover>(c => c.Id.Value == result.Value.Id));
    }

    [Fact]
    public async Task Handle_UsesUtcToday_ForThePastDateRule()
    {
        var yesterday = await _handler.Handle(new CreateCoverCommand(new DateOnly(2026, 10, 13), new DateOnly(2026, 11, 13), CoverType.Tanker), TestContext.Current.CancellationToken);
        var today = await _handler.Handle(new CreateCoverCommand(new DateOnly(2026, 10, 14), new DateOnly(2026, 11, 14), CoverType.Tanker), TestContext.Current.CancellationToken);

        yesterday.Errors.ShouldHaveSingleItem().Code.ShouldBe("Cover.StartDateInPast");
        today.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_InvalidCover_DoesNotRegisterAnything()
    {
        var start = new DateOnly(2026, 11, 1);

        var result = await _handler.Handle(new CreateCoverCommand(start, start.AddYears(2), CoverType.Yacht), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        _covers.DidNotReceiveWithAnyArgs().Add(default!);
    }
}

public sealed class DeleteCoverCommandHandlerTests
{
    private readonly ICoverRepository _covers = Substitute.For<ICoverRepository>();

    [Fact]
    public async Task Handle_ExistingCover_RaisesDeletedEventAndRemovesIt()
    {
        var cover = TestData.NewCover();
        cover.ClearDomainEvents();
        _covers.GetByIdAsync(cover.Id, Arg.Any<CancellationToken>()).Returns(cover);

        var result = await new DeleteCoverCommandHandler(_covers).Handle(new DeleteCoverCommand(cover.Id.Value), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _covers.Received(1).Remove(cover);
        cover.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<CoverDeletedDomainEvent>();
    }

    [Fact]
    public async Task Handle_UnknownCover_ReturnsNotFound()
    {
        var result = await new DeleteCoverCommandHandler(_covers).Handle(new DeleteCoverCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("Cover.NotFound");
    }
}

public sealed class QuotePremiumQueryHandlerTests
{
    private readonly QuotePremiumQueryHandler _handler = new(TestData.PremiumCalculator);

    [Fact]
    public async Task Handle_ValidPeriod_ReturnsQuote()
    {
        var start = new DateOnly(2027, 1, 1);

        var result = await _handler.Handle(new QuotePremiumQuery(start, start.AddDays(180), CoverType.Yacht), TestContext.Current.CancellationToken);

        result.Value.InsuredDays.ShouldBe(180);
        result.Value.Premium.ShouldBe(237_187.50m);
    }

    [Fact]
    public async Task Handle_PeriodLongerThanOneYear_IsRejected()
    {
        var start = new DateOnly(2027, 1, 1);

        var result = await _handler.Handle(new QuotePremiumQuery(start, start.AddDays(400), CoverType.Yacht), TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("Cover.PeriodExceedsOneYear");
    }
}
