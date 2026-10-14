using System.Net;
using System.Net.Http.Json;
using Claims.Api.Contracts;
using Claims.Application.Claims;
using Claims.Application.Common;
using Claims.Domain.Claims;
using Claims.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Claims.IntegrationTests;

public sealed class ClaimsControllerTests(ClaimsApiFactory factory) : ApiTestBase(factory)
{
    // The original template test - now it also checks the payload, not only 200 OK.
    [Fact]
    public async Task Get_Claims()
    {
        var cover = await CreateCoverAsync();
        var claim = await CreateClaimAsync(cover);

        var response = await Client.GetAsync($"{ClaimsUrl}?page=1&pageSize=100", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ClaimResponse>>(Json, Cancellation);
        page.ShouldNotBeNull();
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(100);
        page.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
        page.Items.ShouldContain(item => item.Id == claim.Id);
    }

    [Fact]
    public async Task Post_ValidClaim_Returns201WithLocationAndBody()
    {
        var cover = await CreateCoverAsync();
        var request = new CreateClaimRequest(cover.Id, "Engine fire", ClaimType.Fire, 99_999.99m, cover.StartDate.AddDays(3));

        var response = await Client.PostAsJsonAsync(ClaimsUrl, request, Json, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ClaimResponse>(Json, Cancellation);
        created.ShouldNotBeNull();
        created.CoverId.ShouldBe(cover.Id);
        created.Name.ShouldBe("Engine fire");
        created.Type.ShouldBe(ClaimType.Fire);
        created.DamageCost.ShouldBe(99_999.99m);
        created.Created.ShouldBe(cover.StartDate.AddDays(3));
        response.Headers.Location!.ToString().ShouldEndWith($"{ClaimsUrl}/{created.Id}", Case.Insensitive);
    }

    [Fact]
    public async Task GetById_ReturnsTheStoredClaim()
    {
        var claim = await CreateClaimAsync(await CreateCoverAsync());

        var stored = await Client.GetFromJsonAsync<ClaimResponse>($"{ClaimsUrl}/{claim.Id}", Json, Cancellation);

        stored.ShouldBe(claim);
    }

    [Fact]
    public async Task GetById_UnknownClaim_Returns404Problem()
    {
        var response = await Client.GetAsync($"{ClaimsUrl}/{Guid.NewGuid()}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json, Cancellation);
        problem!.Title.ShouldBe("Claim.NotFound");
    }

    [Fact]
    public async Task Delete_ExistingClaim_Returns204AndRemovesIt()
    {
        var claim = await CreateClaimAsync(await CreateCoverAsync());

        var delete = await Client.DeleteAsync($"{ClaimsUrl}/{claim.Id}", Cancellation);
        var get = await Client.GetAsync($"{ClaimsUrl}/{claim.Id}", Cancellation);

        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_UnknownClaim_Returns404()
    {
        var response = await Client.DeleteAsync($"{ClaimsUrl}/{Guid.NewGuid()}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_DamageCostAbove100000_Returns400WithErrorCode()
    {
        var cover = await CreateCoverAsync();
        var request = new CreateClaimRequest(cover.Id, "Total loss", ClaimType.Grounding, 100_000.01m, cover.StartDate);

        var problem = await PostExpectingValidationProblemAsync(request);

        problem.Errors.ShouldContainKey("damageCost");
        ErrorCodes(problem).ShouldContain("Claim.DamageCostExceedsLimit");
    }

    [Fact]
    public async Task Post_CreatedOutsideCoverPeriod_Returns400WithErrorCode()
    {
        var cover = await CreateCoverAsync(days: 30);
        var request = new CreateClaimRequest(cover.Id, "Too late", ClaimType.BadWeather, 500m, cover.EndDate.AddDays(1));

        var problem = await PostExpectingValidationProblemAsync(request);

        problem.Errors.ShouldContainKey("created");
        ErrorCodes(problem).ShouldContain("Claim.CreatedOutsideCoverPeriod");
    }

    [Fact]
    public async Task Post_UnknownCover_Returns400WithErrorCode()
    {
        var request = new CreateClaimRequest(Guid.NewGuid(), "Orphan", ClaimType.Fire, 500m, Today);

        var problem = await PostExpectingValidationProblemAsync(request);

        ErrorCodes(problem).ShouldContain("Claim.CoverDoesNotExist");
    }

    [Fact]
    public async Task Post_InvalidClaim_IsNotPersisted()
    {
        var cover = await CreateCoverAsync();
        await PostExpectingValidationProblemAsync(new CreateClaimRequest(cover.Id, "Rejected", ClaimType.Fire, 1_000_000m, cover.StartDate));

        var page = await Client.GetFromJsonAsync<PagedResult<ClaimResponse>>($"{ClaimsUrl}?pageSize=100", Json, Cancellation);

        page!.Items.ShouldNotContain(c => c.Name == "Rejected" && c.CoverId == cover.Id);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    public async Task List_WithInvalidPaging_Returns400(string query)
    {
        var response = await Client.GetAsync(ClaimsUrl + query, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<ValidationProblemDetails> PostExpectingValidationProblemAsync(CreateClaimRequest request)
    {
        var response = await Client.PostAsJsonAsync(ClaimsUrl, request, Json, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json, Cancellation))!;
    }

    private static IEnumerable<string> ErrorCodes(ValidationProblemDetails problem) =>
        ((System.Text.Json.JsonElement)problem.Extensions["errorCodes"]!).EnumerateArray().Select(e => e.GetString()!);
}
