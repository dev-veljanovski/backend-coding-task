using Claims.Api.Hosting;
using Claims.Application;
using Claims.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Testcontainers are only used in Development when no connection strings are configured.
await builder.AddLocalContainersAsync();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPresentation()
    .AddRequestLimits(builder.Configuration);

builder.AddObservability();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseRateLimiter();

app.MapApiDocumentation();
app.MapControllers();
app.MapHealthEndpoints();

await app.RunAsync();

public partial class Program
{
}
