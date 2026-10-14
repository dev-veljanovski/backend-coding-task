using Claims.Application;
using Claims.Application.Abstractions.Auditing;
using Claims.Application.Abstractions.Messaging;
using Claims.Application.Abstractions.Persistence;
using Claims.Application.Claims;
using Claims.Application.Claims.CreateClaim;
using Claims.Application.Claims.ListClaims;
using Claims.Application.Common;
using Claims.Application.Covers;
using Claims.Domain.Claims;
using Claims.Domain.Covers;
using Claims.Domain.Results;
using Claims.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Claims.UnitTests.Application;

public sealed class PipelineTests : IAsyncDisposable
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICoverRepository _covers = Substitute.For<ICoverRepository>();
    private readonly IClaimRepository _claims = Substitute.For<IClaimRepository>();
    private readonly IClaimReadService _claimReadService = Substitute.For<IClaimReadService>();
    private readonly ServiceProvider _provider;
    private readonly Cover _cover = TestData.NewCover();

    public PipelineTests()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton(_unitOfWork)
            .AddSingleton(_covers)
            .AddSingleton(_claims)
            .AddSingleton(_claimReadService)
            .AddSingleton(Substitute.For<ICoverReadService>())
            .AddSingleton(Substitute.For<IAuditTrail>());

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _covers.GetByIdAsync(_cover.Id, Arg.Any<CancellationToken>()).Returns(_cover);
    }

    [Fact]
    public async Task Send_SuccessfulCommand_CommitsUnitOfWork()
    {
        var result = await SendAsync(new CreateClaimCommand(_cover.Id.Value, "Valid", ClaimType.Fire, 10m, TestData.Today));

        result.IsSuccess.ShouldBeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_CommandViolatingBusinessRule_DoesNotCommit()
    {
        var result = await SendAsync(new CreateClaimCommand(_cover.Id.Value, "Too expensive", ClaimType.Fire, 1_000_000m, TestData.Today));

        result.IsFailure.ShouldBeTrue();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_MalformedCommand_IsRejectedByValidationBeforeReachingTheHandler()
    {
        var result = await SendAsync(new CreateClaimCommand(Guid.Empty, "", (ClaimType)42, 10m, default));

        result.Errors.Select(e => e.Code).ShouldBe(
            ["Claim.CoverIdRequired", "Claim.NameRequired", "Claim.TypeInvalid", "Claim.CreatedRequired"],
            ignoreOrder: true);
        result.Errors.ShouldAllBe(e => e.Target != null && char.IsLower(e.Target[0]));
        await _covers.DidNotReceive().GetByIdAsync(Arg.Any<CoverId>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_Query_NeverCommits()
    {
        _claimReadService.ListAsync(1, 10, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ClaimResponse>([], 1, 10, 0));

        var result = await SendAsync(new ListClaimsQuery(1, 10));

        result.IsSuccess.ShouldBeTrue();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Send_ListWithInvalidPaging_IsRejected(int page, int pageSize)
    {
        var result = await SendAsync(new ListClaimsQuery(page, pageSize));

        result.IsFailure.ShouldBeTrue();
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    private async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
        where TResponse : IOperationResult<TResponse>
    {
        await using var scope = _provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(request, TestContext.Current.CancellationToken);
    }
}
