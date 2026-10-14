using Asp.Versioning;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Claims.Api.Hosting;

internal static class ApiDocumentationExtensions
{
    private const string DocumentName = "v1";
    private const string EnabledKey = "ApiDocumentation:Enabled";

    private const string Description = """
        Backend for **Insurance Claims Handling**: maintain insurance *covers* for vessels and the *claims* filed under them.

        ### Business rules
        | Resource | Rule |
        |---|---|
        | Claim | `damageCost` must be greater than 0 and cannot exceed **100.000** |
        | Claim | `created` must be within the period of the related cover (bounds inclusive) |
        | Cover | `startDate` cannot be in the past (UTC) |
        | Cover | the total insurance period cannot exceed **1 year** |

        ### Premium
        Base day rate **1250**; surcharge Yacht +10%, Passenger ship +20%, Tanker +50%, other types +30%.
        Days 1–30 at full price; days 31–180 discounted 5% (Yacht) / 2% (others);
        from day 181 an additional 3% (Yacht, 8% in total) / 1% (others, 3% in total).

        ### Errors
        Every error is an [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) `application/problem+json` document.
        Validation errors (`400`) list the messages per field in `errors` and the stable machine-readable codes in `errorCodes`.

        ### Auditing
        Every create and delete is audited asynchronously through a transactional outbox, so requests never wait for the audit store.
        """;

    public static Task DescribeApi(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Info.Title = "Claims API";
        document.Info.Description = Description;
        return Task.CompletedTask;
    }

    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        if (!app.Configuration.GetValue(EnabledKey, defaultValue: true))
        {
            return app;
        }

        app.MapOpenApi().WithDocumentPerVersion();

        app.MapScalarApiReference(options => options.WithTitle("Claims API"));

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint($"/openapi/{DocumentName}.json", "Claims API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "Claims API";
        });

        app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

        return app;
    }
}
