using LmsTutor.Server.Shared.Text;

namespace LmsTutor.UnitTests.Text;

public sealed class TextNormalizerTests
{
    [Theory]
    [InlineData("  ¿Para   qué\n\tsirve POST?  ", "¿Para qué sirve POST?")]
    [InlineData("POST​ sirve\u0007", "POST sirve")]
    [InlineData("ｆｕｌｌ ｗｉｄｔｈ", "full width")]
    [InlineData(null, "")]
    public void CleanUserText_NormalizesWhitespaceAndInvisibleCharacters(string? input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.CleanUserText(input));
    }

    [Fact]
    public void CleanUserText_KeepsMarkupAsLiteralText()
    {
        Assert.Equal("<b>hola</b>", TextNormalizer.CleanUserText("<b>hola</b>"));
    }

    [Fact]
    public void FoldForMatching_RemovesAccentsAndCaseWithoutDependingOnCulture()
    {
        Assert.Equal("explicame que es una peticion idempotente y el nino", TextNormalizer.FoldForMatching("Explícame qué es una petición IDEMPOTENTE y el niño"));
    }
}
