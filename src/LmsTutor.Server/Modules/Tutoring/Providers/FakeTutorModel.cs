using System.Text.RegularExpressions;
using LmsTutor.Server.Shared.ExternalServices;
using LmsTutor.Server.Shared.Text;
using Microsoft.Extensions.Options;

namespace LmsTutor.Server.Modules.Tutoring.Providers;

/// <summary>
/// Deterministic stand-in for the language model, used by the tests, CI and the keyless demo mode.
/// It scores lesson sections by shared keywords and quotes the best one, or reports that the lesson
/// does not cover the question.
/// </summary>
internal sealed partial class FakeTutorModel(IOptions<TutorOptions> options, TimeProvider timeProvider) : ITutorModel
{
    internal const string ModelName = "demo-keyword-model";
    private const int MinimumScore = 2;

    private static readonly HashSet<string> StopWords =
    [
        "que", "para", "por", "como", "cual", "cuales", "cuando", "donde", "quien", "con", "sin", "del", "las", "los",
        "una", "uno", "unos", "unas", "sus", "esta", "este", "esto", "eso", "ese", "esa", "hay", "son", "ser", "mas",
        "muy", "pero", "sobre", "entre", "hace", "puedo", "puede", "debo", "sirve", "sirven", "usa", "usar", "uso", "explica",
        "explicame", "dime", "significa", "diferencia", "and", "the",
    ];

    private static readonly HashSet<string> Greetings = ["hola", "buenas", "gracias", "saludos", "hey", "buenos", "dias", "tardes", "noches"];

    public async Task<TutorModelReply> CompleteAsync(TutorRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (settings.FakeLatencyMilliseconds > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(settings.FakeLatencyMilliseconds), timeProvider, cancellationToken);
        }

        ThrowIfSimulatingFailure(settings.FakeFailure);

        var questionTerms = Terms(request.Question);
        var topics = string.Join(", ", request.Lesson.Sections.Take(3).Select(s => s.Title));

        if (questionTerms.Count > 0 && questionTerms.All(Greetings.Contains))
        {
            return Insufficient($"¡Hola! Estoy aquí para ayudarte con la lección «{request.Lesson.Title}». Puedes preguntarme, por ejemplo, sobre {topics}.");
        }

        var best = request.Lesson.Sections
            .Select(section => (Section: section, Score: Score(section, questionTerms)))
            .OrderByDescending(match => match.Score)
            .First();

        if (best.Score < MinimumScore)
        {
            return Insufficient($"Esta lección no cubre esa pregunta. Puedes preguntarme sobre temas como {topics}.");
        }

        var excerpt = string.Join(' ', SentenceSplitter().Split(best.Section.Body).Take(2)).Trim();
        return new TutorModelReply(TutorReplyStatus.Answered, $"Según la sección «{best.Section.Title}»: {excerpt}", [best.Section.Key], ModelName);
    }

    private static TutorModelReply Insufficient(string answer) =>
        new(TutorReplyStatus.InsufficientInformation, answer, [], ModelName);

    private static int Score(LessonSectionContent section, HashSet<string> questionTerms)
    {
        var titleTerms = Terms(section.Title);
        var bodyTerms = Terms(section.Body);

        // A match in the section title weighs more than a mention in the body.
        return questionTerms.Sum(term => (titleTerms.Contains(term) ? 2 : 0) + (bodyTerms.Contains(term) ? 1 : 0));
    }

    private static HashSet<string> Terms(string text) =>
        WordPattern().Matches(TextNormalizer.FoldForMatching(text))
            .Select(match => match.Value)
            .Where(word => word.Length >= 3 && !StopWords.Contains(word))
            .ToHashSet(StringComparer.Ordinal);

    private static void ThrowIfSimulatingFailure(SimulatedFailure failure)
    {
        switch (failure)
        {
            case SimulatedFailure.RateLimited:
                throw new ExternalServiceException("tutor", ExternalServiceFailure.RateLimited, "Simulated rate limit", TimeSpan.FromSeconds(15));
            case SimulatedFailure.Unavailable:
                throw new ExternalServiceException("tutor", ExternalServiceFailure.Unavailable, "Simulated outage");
            case SimulatedFailure.None:
            default:
                break;
        }
    }

    [GeneratedRegex(@"[a-z0-9]+")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceSplitter();
}
