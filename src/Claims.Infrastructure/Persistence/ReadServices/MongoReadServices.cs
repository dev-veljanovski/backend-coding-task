using Claims.Application.Claims;
using Claims.Application.Common;
using Claims.Application.Covers;
using Claims.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Claims.Infrastructure.Persistence.ReadServices;

internal sealed class MongoClaimReadService(MongoDbContext context) : IClaimReadService
{
    public async Task<ClaimResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var key = id.ToString();
        var document = await context.Claims
            .Find(c => c.Id == key)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return document?.ToResponse();
    }

    public async Task<PagedResult<ClaimResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var filter = Builders<ClaimDocument>.Filter.Empty;

        var totalTask = context.Claims.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var itemsTask = context.Claims
            .Find(filter)
            .SortByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(totalTask, itemsTask).ConfigureAwait(false);

        var items = (await itemsTask.ConfigureAwait(false)).Select(d => d.ToResponse()).ToList();
        return new PagedResult<ClaimResponse>(items, page, pageSize, await totalTask.ConfigureAwait(false));
    }
}

internal sealed class MongoCoverReadService(MongoDbContext context) : ICoverReadService
{
    public async Task<CoverResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var key = id.ToString();
        var document = await context.Covers
            .Find(c => c.Id == key)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return document?.ToResponse();
    }

    public async Task<PagedResult<CoverResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var filter = Builders<CoverDocument>.Filter.Empty;

        var totalTask = context.Covers.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var itemsTask = context.Covers
            .Find(filter)
            .SortByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(totalTask, itemsTask).ConfigureAwait(false);

        var items = (await itemsTask.ConfigureAwait(false)).Select(d => d.ToResponse()).ToList();
        return new PagedResult<CoverResponse>(items, page, pageSize, await totalTask.ConfigureAwait(false));
    }
}
