using LmsTutor.Server.Modules.Tutoring.Providers;
using LmsTutor.Server.Shared.ExternalServices;
using Microsoft.Extensions.Options;

namespace LmsTutor.Server.Modules.Tutoring;

public static class TutoringModule
{
    public static IServiceCollection AddTutoringModule(this IServiceCollection services)
    {
        services.AddOptions<TutorOptions>()
            .BindConfiguration(TutorOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                o => o.Provider != TutorProvider.Groq || !string.IsNullOrWhiteSpace(o.ApiKey),
                "Tutoring:ApiKey is required when Tutoring:Provider is Groq.")
            .ValidateOnStart();

        services.AddScoped<TutorService>();
        services.AddSingleton<FakeTutorModel>();

        services.AddHttpClient<ChatCompletionsTutorModel>((serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<TutorOptions>>().Value;
                client.BaseAddress = new Uri(settings.BaseUrl.EndsWith('/') ? settings.BaseUrl : settings.BaseUrl + "/");
                client.Timeout = Timeout.InfiniteTimeSpan; // the resilience pipeline owns timeouts

                if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    client.DefaultRequestHeaders.Authorization = new("Bearer", settings.ApiKey);
                }
            })
            .AddStandardResilienceHandler(resilience =>
                ProviderResilience.Configure(resilience, attemptTimeout: TimeSpan.FromSeconds(25), totalTimeout: TimeSpan.FromSeconds(55)));

        // The provider is picked per request from options, so configuration (and tests) decide which one runs.
        services.AddScoped<ITutorModel>(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<TutorOptions>>().Value.EffectiveProvider == TutorProvider.Fake
                ? serviceProvider.GetRequiredService<FakeTutorModel>()
                : serviceProvider.GetRequiredService<ChatCompletionsTutorModel>());

        return services;
    }
}
