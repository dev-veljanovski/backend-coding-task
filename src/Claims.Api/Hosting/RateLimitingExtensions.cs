using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

namespace Claims.Api.Hosting;

// The demo runs on a public URL, so every client IP gets a fixed budget of requests per minute.
// Health checks are not limited because the hosting platform calls them constantly.
internal static class RateLimitingExtensions
{
    private const string SectionName = "RateLimiting";

    public static IServiceCollection AddRequestLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue($"{SectionName}:PermitLimit", 60);
        var window = configuration.GetValue($"{SectionName}:Window", TimeSpan.FromMinutes(1));

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
                    ? RateLimitPartition.GetNoLimiter("health")
                    : RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = permitLimit,
                            Window = window,
                            QueueLimit = 0,
                        }));
        });

        return services;
    }
}
