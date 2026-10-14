using System.Net;
using System.Net.Http.Json;
using Claims.Api.Contracts;
using Claims.Application.Covers;
using Claims.Application.Covers.QuotePremium;
using Claims.Domain.Covers;
using Claims.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Claims.IntegrationTests;

public sealed class CoversControllerTests(ClaimsApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Post_ValidCover_Returns201WithComputedPremium()
    {
        var start = Today.AddDays(1);

        var response = await Client.PostAsJsonAsync(CoversUrl, new CreateCoverRequest(start, start.AddDays(180), CoverType.Yacht), Json, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var cover = await response.Content.ReadFromJsonAsync<CoverResponse>(Json, Cancellation);
        cover!.Premium.ShouldBe(237_187.50m);
        response.Headers.Location!.ToString().ShouldEndWith($"{CoversUrl}/{cover.Id}", Case.Insensitive);
    }

    [Fact]
    public async Task Post_StartDateInThePast_Returns400()
    {
        var start = Today.AddDays(-1);

        var response = await Client.PostAsJsonAsync(CoversUrl, new CreateCoverRequest(start, start.AddDays(30), CoverType.Tanker), Json, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json, Cancellation);
        problem!.Errors.ShouldContainKey("startDate");
    }

    [Fact]
    public async Task Post_PeriodLongerThanOneYear_Returns400()
    {
        var start = Today.AddDays(1);

        var response = await Client.PostAsJsonAsync(CoversUrl, new CreateCoverRequest(start, start.AddYears(1).AddDays(1), CoverType.Tanker), Json, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json, Cancellation);
        problem!.Errors.ShouldContainKey("endDate");
    }

    [Fact]
    public async Task Post_UnknownCoverType_Returns400()
    {
        using var content = new StringContent(
            $$"""{"startDate":"{{Today.AddDays(1):yyyy-MM-dd}}","endDate":"{{Today.AddDays(10):yyyy-MM-dd}}","type":"Submarine"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await Client.PostAsync(CoversUrl, content, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_ReturnsTheStoredCover()
    {
        var cover = await CreateCoverAsync(type: CoverType.Tanker);

        var stored = await Client.GetFromJsonAsync<CoverResponse>($"{CoversUrl}/{cover.Id}", Json, Cancellation);

        stored.ShouldBe(cover);
    }

    [Fact]
    public async Task Delete_ExistingCover_Returns204AndRemovesIt()
    {
        var cover = await CreateCoverAsync();

        var delete = await Client.DeleteAsync($"{CoversUrl}/{cover.Id}", Cancellation);
        var get = await Client.GetAsync($"{CoversUrl}/{cover.Id}", Cancellation);

        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task QuotePremium_MatchesThePremiumOfAnIssuedCover()
    {
        var cover = await CreateCoverAsync(days: 250, type: CoverType.PassengerShip);

        var quote = await Client.GetFromJsonAsync<PremiumQuoteResponse>(
            $"{CoversUrl}/premium?startDate={cover.StartDate:yyyy-MM-dd}&endDate={cover.EndDate:yyyy-MM-dd}&coverType=PassengerShip",
            Json,
            Cancellation);

        quote!.InsuredDays.ShouldBe(250);
        quote.Premium.ShouldBe(cover.Premium);
    }

    [Fact]
    public async Task QuotePremium_InvalidPeriod_Returns400()
    {
        var response = await Client.GetAsync($"{CoversUrl}/premium?startDate=2030-01-10&endDate=2030-01-01&coverType=Yacht", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
