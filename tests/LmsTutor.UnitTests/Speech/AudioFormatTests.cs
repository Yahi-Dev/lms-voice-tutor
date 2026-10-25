using LmsTutor.Server.Modules.Speech;

namespace LmsTutor.UnitTests.Speech;

public sealed class AudioFormatTests
{
    [Theory]
    [InlineData("audio/webm", AudioContainer.WebM)]
    [InlineData("audio/webm;codecs=opus", AudioContainer.WebM)]
    [InlineData("audio/ogg; codecs=opus", AudioContainer.Ogg)]
    [InlineData("audio/mp4", AudioContainer.Mp4)]
    [InlineData("AUDIO/WAV", AudioContainer.Wav)]
    public void TryParseDeclaredType_AcceptsBrowserMediaTypesWithParameters(string contentType, AudioContainer expected)
    {
        Assert.True(AudioFormat.TryParseDeclaredType(contentType, out var container));
        Assert.Equal(expected, container);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("video/webm")]
    [InlineData("application/octet-stream")]
    public void TryParseDeclaredType_RejectsOtherTypes(string? contentType)
    {
        Assert.False(AudioFormat.TryParseDeclaredType(contentType, out _));
    }

    [Theory]
    [InlineData(new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x01 }, AudioContainer.WebM)]
    [InlineData(new byte[] { (byte)'O', (byte)'g', (byte)'g', (byte)'S', 0x00 }, AudioContainer.Ogg)]
    [InlineData(new byte[] { 0, 0, 0, 0x1C, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m' }, AudioContainer.Mp4)]
    [InlineData(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0x24, 0, 0, 0, (byte)'W', (byte)'A', (byte)'V', (byte)'E' }, AudioContainer.Wav)]
    public void Detect_RecognizesContainersByTheirLeadingBytes(byte[] content, AudioContainer expected)
    {
        Assert.Equal(expected, AudioFormat.Detect(content));
    }

    [Theory]
    [InlineData(new byte[] { 0x3C, 0x68, 0x74, 0x6D, 0x6C })]
    [InlineData(new byte[] { 0x1A, 0x45 })]
    [InlineData(new byte[0])]
    public void Detect_ReturnsNullForAnythingElse(byte[] content)
    {
        Assert.Null(AudioFormat.Detect(content));
    }
}
