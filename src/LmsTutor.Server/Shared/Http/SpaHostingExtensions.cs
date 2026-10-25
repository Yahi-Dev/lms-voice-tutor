using Microsoft.Net.Http.Headers;

namespace LmsTutor.Server.Shared.Http;

/// <summary>
/// Serves the built SPA from wwwroot (copied there by the Docker build) on the same origin as the API.
/// In development the Vite dev server serves the SPA and proxies /api here instead.
/// </summary>
public static class SpaHostingExtensions
{
    public static WebApplication UseSpaStaticFiles(this WebApplication app)
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                // Vite fingerprints everything under /assets, so those files never change.
                var headers = context.Context.Response.Headers;
                headers.CacheControl = context.Context.Request.Path.StartsWithSegments("/assets")
                    ? "public, max-age=31536000, immutable"
                    : "no-cache";
            },
        });

        return app;
    }

    public static WebApplication MapSpaFallback(this WebApplication app)
    {
        app.MapFallbackToFile("index.html", new StaticFileOptions
        {
            OnPrepareResponse = context => context.Context.Response.Headers[HeaderNames.CacheControl] = "no-cache",
        }).AllowAnonymous();

        return app;
    }
}
