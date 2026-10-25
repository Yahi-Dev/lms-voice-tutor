namespace LmsTutor.Server.Modules.Tutoring;

/// <summary>
/// Server-side rules applied to every model reply, whatever the provider:
/// unknown citations are dropped, an "answered" reply must cite at least one real section, an
/// "insufficient information" reply carries no citations, and the text is kept short and plain.
/// </summary>
internal static class TutorReplyValidator
{
    internal const int MaxAnswerLength = 1200;

    /// <returns>The cleaned reply, or <c>null</c> when it cannot be trusted and should be requested again.</returns>
    public static TutorModelReply? Validate(TutorModelReply reply, IReadOnlyCollection<string> lessonSectionKeys)
    {
        var answer = Clean(reply.Answer);
        if (answer.Length == 0)
        {
            return null;
        }

        if (reply.Status == TutorReplyStatus.InsufficientInformation)
        {
            return reply with { Answer = answer, CitedSectionKeys = [] };
        }

        var citations = reply.CitedSectionKeys
            .Where(lessonSectionKeys.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return citations.Count == 0 ? null : reply with { Answer = answer, CitedSectionKeys = citations };
    }

    private static string Clean(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return string.Empty;
        }

        // The prompt asks for plain text; remove emphasis marks that would otherwise be read aloud.
        var text = answer.Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Trim();

        return text.Length <= MaxAnswerLength ? text : TruncateAtSentence(text);
    }

    private static string TruncateAtSentence(string text)
    {
        var cut = text[..MaxAnswerLength];
        var lastSentenceEnd = cut.LastIndexOfAny(['.', '?', '!']);
        return lastSentenceEnd > MaxAnswerLength / 2 ? cut[..(lastSentenceEnd + 1)] : cut.TrimEnd() + "…";
    }
}
