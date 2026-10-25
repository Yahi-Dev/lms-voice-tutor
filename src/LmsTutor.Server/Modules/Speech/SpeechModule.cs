using LmsTutor.Server.Modules.Speech.Providers;
using LmsTutor.Server.Modules.Tutoring;
using LmsTutor.Server.Shared.ExternalServices;
using Microsoft.Extensions.Options;

namespace LmsTutor.Server.Modules.Speech;

public static class SpeechModule
{
    /// <summary>Server-level ceiling for the audio request; the exact limit is enforced by the endpoint.</summary>
    public const long KestrelBodyLimitBytes = 6_000_000;

    public static IServiceCollection AddSpeechModule(this IServiceCollection services)
    {
        services.AddOptions<SpeechOptions>()
            .BindConfiguration(SpeechOptions.SectionName)
            .PostConfigure<IOptions<TutorOptions>>((speech, tutor) =>
            {
                if (string.IsNullOrWhiteSpace(speech.ApiKey))
                {
                    speech.ApiKey = tutor.Value.ApiKey;
                }
            })
            .ValidateDataAnnotations()
            .Validate(
                o => o.Provider != SpeechProvider.Groq || !string.IsNullOrWhiteSpace(o.ApiKey),
                "Speech:ApiKey (or Tutoring:ApiKey) is required when Speech:Provider is Groq.")
            .ValidateOnStart();

        services.AddSingleton<FakeSpeechTranscriber>();

        services.AddHttpClient<TranscriptionsApiTranscriber>((serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<SpeechOptions>>().Value;
                client.BaseAddress = new Uri(settings.BaseUrl.EndsWith('/') ? settings.BaseUrl : settings.BaseUrl + "/");
                client.Timeout = Timeout.InfiniteTimeSpan; // the resilience pipeline owns timeouts

                if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    client.DefaultRequestHeaders.Authorization = new("Bearer", settings.ApiKey);
                }
            })
            .AddStandardResilienceHandler(resilience =>
                ProviderResilience.Configure(resilience, attemptTimeout: TimeSpan.FromSeconds(30), totalTimeout: TimeSpan.FromSeconds(65)));

        services.AddScoped<ISpeechTranscriber>(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<SpeechOptions>>().Value.EffectiveProvider == SpeechProvider.Fake
                ? serviceProvider.GetRequiredService<FakeSpeechTranscriber>()
                : serviceProvider.GetRequiredService<TranscriptionsApiTranscriber>());

        return services;
    }
}
