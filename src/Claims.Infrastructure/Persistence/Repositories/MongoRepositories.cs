using Claims.Domain.Claims;
using Claims.Domain.Covers;
using Claims.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Claims.Infrastructure.Persistence.Repositories;

internal sealed class MongoClaimRepository(MongoDbContext context, MongoUnitOfWork unitOfWork) : IClaimRepository
{
    public async Task<Claim?> GetByIdAsync(ClaimId id, CancellationToken cancellationToken)
    {
        var key = id.Value.ToString();
        var document = await context.Claims
            .Find(c => c.Id == key)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return document?.ToDomain();
    }

    public void Add(Claim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var document = ClaimDocument.FromDomain(claim);

        unitOfWork.Register(claim, (session, token) =>
            context.Claims.InsertOneAsync(session, document, cancellationToken: token));
    }

    public void Remove(Claim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var id = claim.Id.Value.ToString();

        unitOfWork.Register(claim, (session, token) =>
            context.Claims.DeleteOneAsync(session, c => c.Id == id, cancellationToken: token));
    }
}

internal sealed class MongoCoverRepository(MongoDbContext context, MongoUnitOfWork unitOfWork) : ICoverRepository
{
    public async Task<Cover?> GetByIdAsync(CoverId id, CancellationToken cancellationToken)
    {
        var key = id.Value.ToString();
        var document = await context.Covers
            .Find(c => c.Id == key)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return document?.ToDomain();
    }

    public void Add(Cover cover)
    {
        ArgumentNullException.ThrowIfNull(cover);
        var document = CoverDocument.FromDomain(cover);

        unitOfWork.Register(cover, (session, token) =>
            context.Covers.InsertOneAsync(session, document, cancellationToken: token));
    }

    public void Remove(Cover cover)
    {
        ArgumentNullException.ThrowIfNull(cover);
        var id = cover.Id.Value.ToString();

        unitOfWork.Register(cover, (session, token) =>
            context.Covers.DeleteOneAsync(session, c => c.Id == id, cancellationToken: token));
    }
}
