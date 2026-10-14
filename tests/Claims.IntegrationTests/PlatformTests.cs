using System.Net;
using System.Text.Json;
using Claims.IntegrationTests.Infrastructure;

namespace Claims.IntegrationTests;

public sealed class PlatformTests(ClaimsApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Liveness_IsHealthy()
    {
        var response = await Client.GetAsync("/health/live", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_ReportsEveryDependency()
    {
        var response = await Client.GetAsync("/health/ready", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        var checks = json.RootElement.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();
        checks.ShouldBe(["mongodb", "audit-database", "audit-outbox"], ignoreOrder: true);
    }

    [Fact]
    public async Task OpenApiDocument_DescribesTheVersionedEndpoints()
    {
        var response = await Client.GetAsync("/openapi/v1.json", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = await response.Content.ReadAsStringAsync(Cancellation);
        document.ShouldContain("/api/v1/claims");
        document.ShouldContain("/api/v1/covers");
        document.ShouldContain("/api/v1/covers/premium");
        document.ShouldContain("Claims API");
    }

    [Theory]
    [InlineData("/scalar")]
    [InlineData("/swagger/index.html")]
    public async Task DocumentationUis_AreServed(string url)
    {
        var response = await Client.GetAsync(url, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnknownRoute_ReturnsProblemDetails()
    {
        var response = await Client.GetAsync("/api/v1/does-not-exist", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Responses_ReportSupportedApiVersions()
    {
        var response = await Client.GetAsync("/api/v1/covers", Cancellation);

        response.Headers.GetValues("api-supported-versions").ShouldContain("1.0");
    }
}
