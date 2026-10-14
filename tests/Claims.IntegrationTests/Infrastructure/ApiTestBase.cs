using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Claims.Api.Contracts;
using Claims.Application.Claims;
using Claims.Application.Covers;
using Claims.Domain.Claims;
using Claims.Domain.Covers;

[assembly: AssemblyFixture(typeof(Claims.IntegrationTests.Infrastructure.ClaimsApiFactory))]

namespace Claims.IntegrationTests.Infrastructure;

public abstract class ApiTestBase(ClaimsApiFactory factory)
{
    protected const string ClaimsUrl = "/api/v1/claims";
    protected const string CoversUrl = "/api/v1/covers";

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    protected ClaimsApiFactory Factory { get; } = factory;

    protected HttpClient Client { get; } = factory.CreateClient();

    protected async Task<CoverResponse> CreateCoverAsync(DateOnly? start = null, int days = 90, CoverType type = CoverType.Yacht)
    {
        var startDate = start ?? Today.AddDays(1);
        var response = await Client.PostAsJsonAsync(CoversUrl, new CreateCoverRequest(startDate, startDate.AddDays(days), type), Json, Cancellation);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CoverResponse>(Json, Cancellation))!;
    }

    protected async Task<ClaimResponse> CreateClaimAsync(CoverResponse cover, decimal damageCost = 1_000m)
    {
        var request = new CreateClaimRequest(cover.Id, "Damaged hull", ClaimType.Collision, damageCost, cover.StartDate);
        var response = await Client.PostAsJsonAsync(ClaimsUrl, request, Json, Cancellation);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClaimResponse>(Json, Cancellation))!;
    }

    protected static async Task<bool> EventuallyAsync(Func<Task<bool>> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(100, Cancellation);
        }

        return false;
    }
}
