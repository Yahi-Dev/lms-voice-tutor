using LmsTutor.Server.Modules.Catalog;
using LmsTutor.Server.Modules.Identity;
using LmsTutor.Server.Modules.Speech;
using LmsTutor.Server.Modules.Tutoring;
using LmsTutor.Server.Shared.Http;
using LmsTutor.Server.Shared.Persistence;
using LmsTutor.Server.Shared.RateLimiting;
using LmsTutor.Server.Shared.Security;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
    kestrel.Limits.MaxRequestBodySize = SpeechModule.KestrelBodyLimitBytes;
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddValidation();
builder.Services.AddApiErrorHandling();
builder.Services.AddApiRateLimiting();
builder.Services.AddPersistence();
builder.Services.AddPersistentKeyRing(builder.Configuration);
builder.Services.AddIdentityModule();
builder.Services.AddTutoringModule();
builder.Services.AddSpeechModule();

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseSpaStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<CsrfProtectionMiddleware>();

var api = app.MapGroup("/api").DisableCookieRedirect();

api.MapAuthEndpoints();
api.MapCatalogEndpoints();
api.MapSystemStatusEndpoints();
api.MapLessonGroup()
    .MapTutoringEndpoints()
    .MapSpeechEndpoints();

// Unknown API routes are 404s, never the SPA's index.html.
api.Map("/{**path}", () => TypedResults.NotFound());

app.MapHealthChecks("/health").AllowAnonymous();

app.MapSpaFallback();

app.Run();
