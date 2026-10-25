using LmsTutor.Server.Modules.Tutoring;

namespace LmsTutor.IntegrationTests.Infrastructure;

/// <summary>Scripted tutor model for failure scenarios.</summary>
internal sealed class StubTutorModel(Func<TutorRequest, TutorModelReply> respond) : ITutorModel
{
    public int Calls { get; private set; }

    public Task<TutorModelReply> CompleteAsync(TutorRequest request, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(respond(request));
    }
}
