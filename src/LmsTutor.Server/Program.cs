using LmsTutor.Server.Modules.Catalog;
using LmsTutor.Server.Modules.Identity;
using LmsTutor.Server.Shared.Http;
using LmsTutor.Server.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidation();
builder.Services.AddApiErrorHandling();
builder.Services.AddPersistence();
builder.Services.AddIdentityModule();

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CsrfProtectionMiddleware>();

var api = app.MapGroup("/api").DisableCookieRedirect();

api.MapAuthEndpoints();
api.MapCatalogEndpoints();
api.MapLessonGroup();

// Unknown API routes are 404s, never the SPA's index.html.
api.Map("/{**path}", () => TypedResults.NotFound());

app.MapHealthChecks("/health").AllowAnonymous();

app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();
