using System.ComponentModel;
using Asp.Versioning;
using Claims.Api.Contracts;
using Claims.Application.Abstractions.Messaging;
using Claims.Application.Common;
using Claims.Application.Covers;
using Claims.Application.Covers.CreateCover;
using Claims.Application.Covers.DeleteCover;
using Claims.Application.Covers.GetCover;
using Claims.Application.Covers.ListCovers;
using Claims.Application.Covers.QuotePremium;
using Claims.Domain.Covers;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Controllers;

// Task 1: same as ClaimsController. The premium computation that used to live
// here moved to the domain (PremiumCalculator) - see Task 5.
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/covers")]
[Tags("Covers")]
public sealed class CoversController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    [EndpointSummary("List covers")]
    [EndpointDescription("Returns one page of covers, newest first.")]
    [ProducesResponseType<PagedResult<CoverResponse>>(StatusCodes.Status200OK, Description = "One page of covers.")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json", Description = "Invalid paging parameters.")]
    public async Task<IActionResult> List(
        [FromQuery, Description("1-based page number.")] int page = 1,
        [FromQuery, Description("Covers per page (1-100).")] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListCoversQuery(page, pageSize), cancellationToken);
        return result.Match<IActionResult>(Ok, ToProblem);
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get a cover")]
    [ProducesResponseType<CoverResponse>(StatusCodes.Status200OK, Description = "The cover.")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json", Description = "No cover with this id exists.")]
    public async Task<IActionResult> GetById([Description("Id of the cover.")] Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCoverQuery(id), cancellationToken);
        return result.Match<IActionResult>(Ok, ToProblem);
    }

    [HttpPost]
    [EndpointSummary("Issue a cover")]
    [EndpointDescription("Issues a new cover; the premium is computed by the service. Business rules: `startDate` cannot be in the past and the total insurance period cannot exceed 1 year. The creation is audited asynchronously.")]
    [Consumes("application/json")]
    [ProducesResponseType<CoverResponse>(StatusCodes.Status201Created, Description = "The cover was created; its URL is in the Location header.")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json", Description = "The request is malformed or breaks a business rule.")]
    public async Task<IActionResult> Create([FromBody] CreateCoverRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await sender.Send(new CreateCoverCommand(request.StartDate, request.EndDate, request.Type), cancellationToken);

        return result.Match<IActionResult>(
            cover => CreatedAtAction(nameof(GetById), new { id = cover.Id }, cover),
            ToProblem);
    }

    [HttpDelete("{id:guid}")]
    [EndpointSummary("Delete a cover")]
    [EndpointDescription("Deletes a cover. The deletion is audited asynchronously.")]
    [ProducesResponseType(StatusCodes.Status204NoContent, Description = "The cover was deleted.")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json", Description = "No cover with this id exists.")]
    public async Task<IActionResult> Delete([Description("Id of the cover.")] Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteCoverCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToProblem(result.Errors);
    }

    [HttpGet("premium")]
    [EndpointSummary("Quote a premium")]
    [EndpointDescription("Computes the premium of a prospective cover without creating it. Uses exactly the same pricing as cover creation.")]
    [ProducesResponseType<PremiumQuoteResponse>(StatusCodes.Status200OK, Description = "The quote.")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json", Description = "The period is invalid.")]
    public async Task<IActionResult> QuotePremium(
        [FromQuery, Description("First insured day (yyyy-MM-dd).")] DateOnly startDate,
        [FromQuery, Description("Last insured day (yyyy-MM-dd), at most 1 year after the start date.")] DateOnly endDate,
        [FromQuery, Description("The insured vessel type.")] CoverType coverType,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new QuotePremiumQuery(startDate, endDate, coverType), cancellationToken);
        return result.Match<IActionResult>(Ok, ToProblem);
    }
}
