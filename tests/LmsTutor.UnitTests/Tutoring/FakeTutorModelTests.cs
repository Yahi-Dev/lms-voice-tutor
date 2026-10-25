using LmsTutor.Server.Modules.Tutoring;
using LmsTutor.Server.Modules.Tutoring.Providers;
using LmsTutor.Server.Shared.ExternalServices;
using Microsoft.Extensions.Options;

namespace LmsTutor.UnitTests.Tutoring;

public sealed class FakeTutorModelTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("¿Para qué sirve POST?", "post")]
    [InlineData("¿Qué hace DELETE?", "delete")]
    [InlineData("explícame get", "get")]
    public async Task Complete_QuestionCoveredByASection_AnswersFromThatSection(string question, string expectedKey)
    {
        var reply = await CreateModel().CompleteAsync(new TutorRequest(TestLessons.HttpMethods, [], question), _ct);

        Assert.Equal(TutorReplyStatus.Answered, reply.Status);
        Assert.Equal([expectedKey], reply.CitedSectionKeys);
    }

    [Theory]
    [InlineData("¿Quién ganó el Mundial 2022?")]
    [InlineData("¿Cómo despliego en Kubernetes?")]
    public async Task Complete_QuestionOutsideTheLesson_ReportsInsufficientInformation(string question)
    {
        var reply = await CreateModel().CompleteAsync(new TutorRequest(TestLessons.HttpMethods, [], question), _ct);

        Assert.Equal(TutorReplyStatus.InsufficientInformation, reply.Status);
        Assert.Empty(reply.CitedSectionKeys);
    }

    [Fact]
    public async Task Complete_Greeting_GetsAFriendlyRedirectToTheLesson()
    {
        var reply = await CreateModel().CompleteAsync(new TutorRequest(TestLessons.HttpMethods, [], "¡Hola!"), _ct);

        Assert.Equal(TutorReplyStatus.InsufficientInformation, reply.Status);
        Assert.Contains("Métodos HTTP", reply.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Complete_WhenSimulatingAnOutage_ThrowsAProviderFailure()
    {
        var model = CreateModel(SimulatedFailure.Unavailable);

        var failure = await Assert.ThrowsAsync<ExternalServiceException>(() =>
            model.CompleteAsync(new TutorRequest(TestLessons.HttpMethods, [], "¿Para qué sirve POST?"), _ct));

        Assert.Equal(ExternalServiceFailure.Unavailable, failure.Failure);
    }

    private static FakeTutorModel CreateModel(SimulatedFailure failure = SimulatedFailure.None) =>
        new(Options.Create(new TutorOptions { FakeLatencyMilliseconds = 0, FakeFailure = failure }), TimeProvider.System);
}
