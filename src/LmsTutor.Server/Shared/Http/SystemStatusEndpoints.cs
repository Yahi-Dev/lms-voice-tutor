using LmsTutor.Server.Modules.Speech;
using LmsTutor.Server.Modules.Tutoring;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace LmsTutor.Server.Shared.Http;

public sealed record SystemStatusResponse(string TutorProvider, string SpeechProvider, bool DemoMode);

public static class SystemStatusEndpoints
{
    /// <summary>Tells the SPA whether it is talking to real providers or to the built-in demo ones.</summary>
    public static RouteGroupBuilder MapSystemStatusEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/system/status", GetStatus).WithTags("System");
        return api;
    }

    private static Ok<SystemStatusResponse> GetStatus(IOptions<TutorOptions> tutorOptions, IOptions<SpeechOptions> speechOptions)
    {
        var tutorDemo = tutorOptions.Value.EffectiveProvider == TutorProvider.Fake;
        var speechDemo = speechOptions.Value.EffectiveProvider == SpeechProvider.Fake;

        return TypedResults.Ok(new SystemStatusResponse(
            tutorDemo ? "demo" : "groq",
            speechDemo ? "demo" : "groq",
            DemoMode: tutorDemo || speechDemo));
    }
}
