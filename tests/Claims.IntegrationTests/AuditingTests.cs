using Claims.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Claims.IntegrationTests;

// Task 3: audit rows are written asynchronously, so these tests poll for them.
public sealed class AuditingTests(ClaimsApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task CreatingAndDeletingAClaim_IsAuditedAsPostAndDelete()
    {
        var claim = await CreateClaimAsync(await CreateCoverAsync());
        var claimId = claim.Id.ToString();

        (await Client.DeleteAsync($"{ClaimsUrl}/{claim.Id}", Cancellation)).EnsureSuccessStatusCode();

        var audited = await EventuallyAsync(async () =>
        {
            var verbs = await Factory.QueryAuditAsync(db => db.ClaimAudits
                .Where(a => a.ClaimId == claimId)
                .Select(a => a.HttpRequestType)
                .ToListAsync(Cancellation));
            return verbs.Contains("POST") && verbs.Contains("DELETE");
        });

        audited.ShouldBeTrue("the claim should be audited as POST and DELETE");
    }

    [Fact]
    public async Task CreatingAndDeletingACover_IsAuditedAsPostAndDelete()
    {
        var cover = await CreateCoverAsync();
        var coverId = cover.Id.ToString();

        (await Client.DeleteAsync($"{CoversUrl}/{cover.Id}", Cancellation)).EnsureSuccessStatusCode();

        var audited = await EventuallyAsync(async () =>
        {
            var verbs = await Factory.QueryAuditAsync(db => db.CoverAudits
                .Where(a => a.CoverId == coverId)
                .Select(a => a.HttpRequestType)
                .ToListAsync(Cancellation));
            return verbs.Contains("POST") && verbs.Contains("DELETE");
        });

        audited.ShouldBeTrue("the cover should be audited as POST and DELETE");
    }

    [Fact]
    public async Task EveryAuditEntry_IsWrittenExactlyOnce()
    {
        var cover = await CreateCoverAsync();
        var coverId = cover.Id.ToString();

        await EventuallyAsync(async () =>
            await Factory.QueryAuditAsync(db => db.CoverAudits.AnyAsync(a => a.CoverId == coverId, Cancellation)));

        await Task.Delay(TimeSpan.FromSeconds(1), Cancellation);
        var entries = await Factory.QueryAuditAsync(db => db.CoverAudits.CountAsync(a => a.CoverId == coverId, Cancellation));

        entries.ShouldBe(1);
    }
}
