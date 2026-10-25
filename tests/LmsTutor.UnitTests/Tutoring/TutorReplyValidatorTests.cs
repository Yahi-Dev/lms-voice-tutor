using LmsTutor.Server.Modules.Tutoring;

namespace LmsTutor.UnitTests.Tutoring;

public sealed class TutorReplyValidatorTests
{
    private static readonly HashSet<string> LessonKeys = ["get", "post", "delete"];

    [Fact]
    public void Validate_DropsCitationsThatAreNotInTheLesson()
    {
        var reply = Reply(TutorReplyStatus.Answered, "POST crea recursos.", "post", "invented", "post");

        var result = TutorReplyValidator.Validate(reply, LessonKeys);

        Assert.Equal(["post"], result!.CitedSectionKeys);
    }

    [Fact]
    public void Validate_RejectsAnAnswerWithoutAnyValidCitation()
    {
        var reply = Reply(TutorReplyStatus.Answered, "Una respuesta inventada.", "invented");

        Assert.Null(TutorReplyValidator.Validate(reply, LessonKeys));
    }

    [Fact]
    public void Validate_RemovesCitationsFromAnInsufficientInformationReply()
    {
        var reply = Reply(TutorReplyStatus.InsufficientInformation, "La lección no cubre eso.", "post");

        var result = TutorReplyValidator.Validate(reply, LessonKeys);

        Assert.Empty(result!.CitedSectionKeys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsAnEmptyAnswer(string answer)
    {
        Assert.Null(TutorReplyValidator.Validate(Reply(TutorReplyStatus.Answered, answer, "post"), LessonKeys));
    }

    [Fact]
    public void Validate_StripsEmphasisMarksThatWouldBeReadAloud()
    {
        var reply = Reply(TutorReplyStatus.Answered, "**POST** crea un recurso con `201 Created`.", "post");

        var result = TutorReplyValidator.Validate(reply, LessonKeys);

        Assert.Equal("POST crea un recurso con 201 Created.", result!.Answer);
    }

    [Fact]
    public void Validate_TruncatesVeryLongAnswersAtASentenceBoundary()
    {
        var longAnswer = string.Concat(Enumerable.Repeat("Esta es una oración de relleno para la prueba. ", 60));

        var result = TutorReplyValidator.Validate(Reply(TutorReplyStatus.Answered, longAnswer, "post"), LessonKeys);

        Assert.True(result!.Answer.Length <= TutorReplyValidator.MaxAnswerLength);
        Assert.EndsWith(".", result.Answer, StringComparison.Ordinal);
    }

    private static TutorModelReply Reply(TutorReplyStatus status, string answer, params string[] citations) =>
        new(status, answer, citations, "test-model");
}
