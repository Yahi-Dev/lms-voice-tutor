using Microsoft.Net.Http.Headers;

namespace LmsTutor.Server.Modules.Speech;

/// <summary>
/// Validates uploaded audio by its declared media type and by its leading bytes, so a renamed file of
/// another kind is rejected before it reaches the speech provider.
/// </summary>
internal static class AudioFormat
{
    private static readonly Dictionary<string, AudioContainer> DeclaredTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/webm"] = AudioContainer.WebM,
        ["audio/ogg"] = AudioContainer.Ogg,
        ["audio/mp4"] = AudioContainer.Mp4,
        ["audio/x-m4a"] = AudioContainer.Mp4,
        ["audio/m4a"] = AudioContainer.Mp4,
        ["audio/wav"] = AudioContainer.Wav,
        ["audio/x-wav"] = AudioContainer.Wav,
        ["audio/wave"] = AudioContainer.Wav,
    };

    /// <summary>
    /// Reads only the media type: browsers send parameters such as <c>audio/webm;codecs=opus</c>.
    /// </summary>
    public static bool TryParseDeclaredType(string? contentType, out AudioContainer container)
    {
        container = default;
        return MediaTypeHeaderValue.TryParse(contentType, out var parsed)
            && parsed.MediaType.HasValue
            && DeclaredTypes.TryGetValue(parsed.MediaType.Value!, out container);
    }

    public static AudioContainer? Detect(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 4 && content[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }))
        {
            return AudioContainer.WebM;
        }

        if (content.Length >= 4 && content[..4].SequenceEqual("OggS"u8))
        {
            return AudioContainer.Ogg;
        }

        // ISO base media (MP4/M4A): the 'ftyp' box name follows the 4-byte box size.
        if (content.Length >= 8 && content.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            return AudioContainer.Mp4;
        }

        if (content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            return AudioContainer.Wav;
        }

        return null;
    }
}
