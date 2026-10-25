using LmsTutor.Server.Modules.Tutoring;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace LmsTutor.Server.Shared.Http;

public sealed record SystemStatusResponse(string TutorProvider, bool DemoMode);

public static class SystemStatusEndpoints
{
    /// <summary>Tells the SPA whether it is talking to a real model or to the built-in demo model.</summary>
    public static RouteGroupBuilder MapSystemStatusEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/system/status", GetStatus).WithTags("System");
        return api;
    }

    private static Ok<SystemStatusResponse> GetStatus(IOptions<TutorOptions> tutorOptions)
    {
        var demoMode = tutorOptions.Value.EffectiveProvider == TutorProvider.Fake;
        return TypedResults.Ok(new SystemStatusResponse(demoMode ? "demo" : "groq", demoMode));
    }
}
