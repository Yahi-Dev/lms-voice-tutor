using LmsTutor.Server.Shared.Http;
using LmsTutor.Server.Shared.Persistence;
using LmsTutor.Server.Shared.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LmsTutor.Server.Modules.Speech;

public sealed record TranscriptionResponse(string Text);

public static class SpeechEndpoints
{
    /// <summary>
    /// Maps transcription under the lesson group, so only enrolled students can use it.
    /// The audio is sent as the raw request body (not multipart) and never touches the disk.
    /// </summary>
    public static RouteGroupBuilder MapSpeechEndpoints(this RouteGroupBuilder lessons)
    {
        lessons.MapPost("/transcriptions", TranscribeAsync)
            .WithTags("Speech")
            .RequireRateLimiting(RateLimitPolicies.Speech)
            .WithMetadata(new RequestSizeLimitAttribute(SpeechModule.KestrelBodyLimitBytes));

        return lessons;
    }

    private static async Task<Results<Ok<TranscriptionResponse>, ProblemHttpResult>> TranscribeAsync(
        Guid lessonId,
        HttpRequest request,
        ISpeechTranscriber transcriber,
        AppDbContext db,
        IOptions<SpeechOptions> options,
        CancellationToken cancellationToken)
    {
        var maxBytes = options.Value.MaxUploadBytes;

        if (!AudioFormat.TryParseDeclaredType(request.ContentType, out var declaredContainer))
        {
            return UnsupportedFormat();
        }

        if (request.ContentLength > maxBytes)
        {
            return TooLarge();
        }

        // Enforced here as well: Content-Length may be missing (chunked uploads) and test hosts skip Kestrel's limit.
        var audio = await ReadBoundedAsync(request.Body, maxBytes, cancellationToken);
        if (audio is null)
        {
            return TooLarge();
        }

        if (audio.Value.IsEmpty)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "audio_empty", "No se recibió audio.");
        }

        if (AudioFormat.Detect(audio.Value.Span) != declaredContainer)
        {
            return UnsupportedFormat();
        }

        var hint = await BuildVocabularyHintAsync(db, lessonId, cancellationToken);
        var transcript = await transcriber.TranscribeAsync(new AudioClip(audio.Value, declaredContainer), hint, cancellationToken);

        return transcript.SpeechDetected
            ? TypedResults.Ok(new TranscriptionResponse(transcript.Text))
            : ApiProblem.Create(StatusCodes.Status422UnprocessableEntity, "no_speech_detected",
                "No se detectó voz en la grabación. Intenta hablar más cerca del micrófono.");
    }

    /// <returns>The body, or <c>null</c> when it exceeds <paramref name="maxBytes"/>.</returns>
    private static async Task<ReadOnlyMemory<byte>?> ReadBoundedAsync(Stream body, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;

        while ((read = await body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    // Lesson and section titles help the recognizer spell terms such as "POST" or "endpoint".
    private static async Task<string> BuildVocabularyHintAsync(AppDbContext db, Guid lessonId, CancellationToken cancellationToken)
    {
        var lesson = await db.Lessons.AsNoTracking()
            .Where(l => l.Id == lessonId)
            .Select(l => new { l.Title, Sections = l.Sections.OrderBy(s => s.Position).Select(s => s.Title).ToList() })
            .FirstAsync(cancellationToken);

        return $"Pregunta sobre la lección «{lesson.Title}»: {string.Join(", ", lesson.Sections)}.";
    }

    private static ProblemHttpResult UnsupportedFormat() =>
        ApiProblem.Create(StatusCodes.Status415UnsupportedMediaType, "unsupported_audio_format",
            "Formato de audio no soportado. Usa WebM, Ogg, MP4 o WAV.");

    private static ProblemHttpResult TooLarge() =>
        ApiProblem.Create(StatusCodes.Status413PayloadTooLarge, "audio_too_large",
            "La grabación es demasiado larga. Intenta con una pregunta más corta.");
}
