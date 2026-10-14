using System.ComponentModel;
using Asp.Versioning;
using Claims.Api.Contracts;
using Claims.Application.Abstractions.Messaging;
using Claims.Application.Claims;
using Claims.Application.Claims.CreateClaim;
using Claims.Application.Claims.DeleteClaim;
using Claims.Application.Claims.GetClaim;
using Claims.Application.Claims.ListClaims;
using Claims.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Controllers;

// Task 1: the controller only maps HTTP to commands/queries. Validation, business rules,
// persistence and auditing live in the Application/Domain/Infrastructure layers.
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/claims")]
[Tags("Claims")]
public sealed class ClaimsController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    [EndpointSummary("List claims")]
    [EndpointDescription("Returns one page of claims, newest first.")]
    [ProducesResponseType<PagedResult<ClaimResponse>>(StatusCodes.Status200OK, Description = "One page of claims.")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json", Description = "Invalid paging parameters.")]
    public async Task<IActionResult> List(
        [FromQuery, Description("1-based page number.")] int page = 1,
        [FromQuery, Description("Claims per page (1-100).")] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListClaimsQuery(page, pageSize), cancellationToken);
        return result.Match<IActionResult>(Ok, ToProblem);
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get a claim")]
    [ProducesResponseType<ClaimResponse>(StatusCodes.Status200OK, Description = "The claim.")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json", Description = "No claim with this id exists.")]
    public async Task<IActionResult> GetById([Description("Id of the claim.")] Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetClaimQuery(id), cancellationToken);
        return result.Match<IActionResult>(Ok, ToProblem);
    }

    [HttpPost]
    [EndpointSummary("File a claim")]
    [EndpointDescription("Files a new claim under an existing cover. Business rules: `damageCost` must be greater than 0 and cannot exceed 100.000; `created` must be within the period of the cover. The creation is audited asynchronously.")]
    [Consumes("application/json")]
    [ProducesResponseType<ClaimResponse>(StatusCodes.Status201Created, Description = "The claim was created; its URL is in the Location header.")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json", Description = "The request is malformed or breaks a business rule.")]
    public async Task<IActionResult> Create([FromBody] CreateClaimRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new CreateClaimCommand(request.CoverId, request.Name, request.Type, request.DamageCost, request.Created);
        var result = await sender.Send(command, cancellationToken);

        return result.Match<IActionResult>(
            claim => CreatedAtAction(nameof(GetById), new { id = claim.Id }, claim),
            ToProblem);
    }

    [HttpDelete("{id:guid}")]
    [EndpointSummary("Delete a claim")]
    [EndpointDescription("Deletes a claim. The deletion is audited asynchronously.")]
    [ProducesResponseType(StatusCodes.Status204NoContent, Description = "The claim was deleted.")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json", Description = "No claim with this id exists.")]
    public async Task<IActionResult> Delete([Description("Id of the claim.")] Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteClaimCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : ToProblem(result.Errors);
    }
}
