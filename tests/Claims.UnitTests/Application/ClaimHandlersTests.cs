using Claims.Application.Claims;
using Claims.Application.Claims.CreateClaim;
using Claims.Application.Claims.DeleteClaim;
using Claims.Application.Claims.GetClaim;
using Claims.Domain.Claims;
using Claims.Domain.Claims.Events;
using Claims.Domain.Covers;
using Claims.UnitTests.TestDoubles;
using NSubstitute;

namespace Claims.UnitTests.Application;

public sealed class CreateClaimCommandHandlerTests
{
    private readonly ICoverRepository _covers = Substitute.For<ICoverRepository>();
    private readonly IClaimRepository _claims = Substitute.For<IClaimRepository>();
    private readonly CreateClaimCommandHandler _handler;
    private readonly Cover _cover = TestData.NewCover(TestData.Today, TestData.Today.AddDays(60));

    public CreateClaimCommandHandlerTests()
    {
        _handler = new CreateClaimCommandHandler(_covers, _claims);
        _covers.GetByIdAsync(_cover.Id, Arg.Any<CancellationToken>()).Returns(_cover);
    }

    [Fact]
    public async Task Handle_ValidClaim_RegistersClaimAndReturnsIt()
    {
        var command = new CreateClaimCommand(_cover.Id.Value, "Collision in port", ClaimType.Collision, 5_000m, TestData.Today.AddDays(1));

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.CoverId.ShouldBe(_cover.Id.Value);
        result.Value.DamageCost.ShouldBe(5_000m);
        _claims.Received(1).Add(Arg.Is<Claim>(c => c.Id.Value == result.Value.Id));
    }

    [Fact]
    public async Task Handle_UnknownCover_FailsWithoutRegisteringAnything()
    {
        var command = new CreateClaimCommand(Guid.NewGuid(), "Orphan", ClaimType.Fire, 10m, TestData.Today);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("Claim.CoverDoesNotExist");
        _claims.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task Handle_BusinessRuleViolation_FailsWithoutRegisteringAnything()
    {
        var command = new CreateClaimCommand(_cover.Id.Value, "Too much", ClaimType.Fire, 100_001m, TestData.Today);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("Claim.DamageCostExceedsLimit");
        _claims.DidNotReceiveWithAnyArgs().Add(default!);
    }
}

public sealed class DeleteClaimCommandHandlerTests
{
    private readonly IClaimRepository _claims = Substitute.For<IClaimRepository>();

    [Fact]
    public async Task Handle_ExistingClaim_RaisesDeletedEventAndRemovesIt()
    {
        var claim = Claim.File(TestData.NewCover(), "Existing", ClaimType.Fire, 10m, TestData.Today).Value;
        claim.ClearDomainEvents();
        _claims.GetByIdAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);

        var result = await new DeleteClaimCommandHandler(_claims).Handle(new DeleteClaimCommand(claim.Id.Value), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _claims.Received(1).Remove(claim);
        claim.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ClaimDeletedDomainEvent>();
    }

    [Fact]
    public async Task Handle_UnknownClaim_ReturnsNotFound()
    {
        var result = await new DeleteClaimCommandHandler(_claims).Handle(new DeleteClaimCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("Claim.NotFound");
        _claims.DidNotReceiveWithAnyArgs().Remove(default!);
    }
}

public sealed class GetClaimQueryHandlerTests
{
    private readonly IClaimReadService _readService = Substitute.For<IClaimReadService>();

    [Fact]
    public async Task Handle_ExistingClaim_ReturnsIt()
    {
        var response = new ClaimResponse(Guid.NewGuid(), Guid.NewGuid(), "Name", ClaimType.Fire, 10m, TestData.Today);
        _readService.GetByIdAsync(response.Id, Arg.Any<CancellationToken>()).Returns(response);

        var result = await new GetClaimQueryHandler(_readService).Handle(new GetClaimQuery(response.Id), TestContext.Current.CancellationToken);

        result.Value.ShouldBe(response);
    }

    [Fact]
    public async Task Handle_UnknownClaim_ReturnsNotFound()
    {
        var result = await new GetClaimQueryHandler(_readService).Handle(new GetClaimQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("Claim.NotFound");
    }
}
