using Claims.Domain.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Claims.Api.Controllers;

// No [Produces("application/json")] here on purpose - it would also override application/problem+json on errors.
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ToProblem(IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count > 0 && errors.All(error => error.Type == ErrorType.Validation))
        {
            var modelState = new ModelStateDictionary();
            foreach (var error in errors)
            {
                modelState.AddModelError(error.Target ?? string.Empty, error.Message);
            }

            var validationProblem = ProblemDetailsFactory.CreateValidationProblemDetails(
                HttpContext,
                modelState,
                StatusCodes.Status400BadRequest,
                title: "One or more business rules were violated.");
            validationProblem.Extensions["errorCodes"] = errors.Select(error => error.Code).ToArray();

            return new ObjectResult(validationProblem)
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" },
            };
        }

        var first = errors.FirstOrDefault(error => error.Type != ErrorType.Validation)
            ?? throw new InvalidOperationException("A failed result must contain at least one error.");

        var status = first.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, title: first.Code, detail: first.Message);
        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }
}
