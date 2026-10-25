using System.Text;

namespace LmsTutor.Server.Shared.Text;

public static class TextNormalizer
{
    /// <summary>
    /// Cleans free text typed or dictated by a student: Unicode compatibility normalization, no control or
    /// zero-width characters, collapsed whitespace. The text is stored as-is otherwise; HTML encoding is the
    /// renderer's job, not the storage's.
    /// </summary>
    public static string CleanUserText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(normalized.Length);
        var pendingSpace = false;

        foreach (var character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (char.IsControl(character) || IsZeroWidth(character))
            {
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Lowercase ASCII-folded form for keyword matching. It uses an explicit map instead of Unicode
    /// decomposition so it behaves the same in containers that run with invariant globalization.
    /// </summary>
    public static string FoldForMatching(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(char.ToLowerInvariant(character) switch
            {
                'á' or 'à' or 'ä' or 'â' => 'a',
                'é' or 'è' or 'ë' or 'ê' => 'e',
                'í' or 'ì' or 'ï' or 'î' => 'i',
                'ó' or 'ò' or 'ö' or 'ô' => 'o',
                'ú' or 'ù' or 'ü' or 'û' => 'u',
                'ñ' => 'n',
                var other => other,
            });
        }

        return builder.ToString();
    }

    private static bool IsZeroWidth(char character) =>
        character is '​' or '‌' or '‍' or '⁠' or '﻿';
}
