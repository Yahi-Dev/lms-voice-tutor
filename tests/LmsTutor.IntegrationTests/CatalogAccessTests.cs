using System.Net;
using System.Net.Http.Json;
using LmsTutor.IntegrationTests.Infrastructure;
using LmsTutor.Server.Modules.Catalog;
using LmsTutor.Server.Shared.Persistence.Seed;

namespace LmsTutor.IntegrationTests;

public sealed class CatalogAccessTests(TutorApiFactory factory) : IClassFixture<TutorApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Courses_ForEnrolledStudent_ListTheCourseWithItsTwoLessons()
    {
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Ana);

        var courses = await client.GetFromJsonAsync<List<CourseSummaryResponse>>("/api/courses", _ct);

        var course = Assert.Single(courses!);
        Assert.Equal(DemoData.CourseSlug, course.Slug);
        Assert.Equal(["metodos-http", "codigos-de-estado"], course.Lessons.Select(l => l.Slug));
    }

    [Fact]
    public async Task Courses_ForStudentWithoutAccess_AreEmpty()
    {
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Bruno);

        var courses = await client.GetFromJsonAsync<List<CourseSummaryResponse>>("/api/courses", _ct);

        Assert.Empty(courses!);
    }

    [Fact]
    public async Task Lesson_ForEnrolledStudent_ReturnsItsSectionsInOrder()
    {
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Ana);

        var lesson = await client.GetFromJsonAsync<LessonResponse>($"/api/lessons/{lessonId}", _ct);

        Assert.Equal("Métodos HTTP", lesson!.Title);
        Assert.Equal("APIs REST desde cero", lesson.CourseTitle);
        Assert.Equal("que-es-un-metodo", lesson.Sections[0].Key);
        Assert.Contains(lesson.Sections, s => s.Key == "post");
    }

    [Fact]
    public async Task Lesson_ForStudentWithoutAccess_Returns403()
    {
        var lessonId = await factory.GetLessonIdAsync("metodos-http");
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Bruno);

        var response = await client.GetAsync($"/api/lessons/{lessonId}", _ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("lesson_forbidden", await response.ReadProblemCodeAsync());
        Assert.DoesNotContain("POST sirve", await response.Content.ReadAsStringAsync(_ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lesson_ThatDoesNotExist_Returns404()
    {
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Ana);

        var response = await client.GetAsync($"/api/lessons/{Guid.CreateVersion7()}", _ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("lesson_not_found", await response.ReadProblemCodeAsync());
    }

    [Theory]
    [InlineData("/api/courses")]
    [InlineData("/api/lessons/01a1744c-6faf-7f9c-a0e6-e71f4855324f")]
    public async Task ProtectedEndpoints_WithoutSession_Return401(string path)
    {
        var client = factory.CreateApiClient();

        var response = await client.GetAsync(path, _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownApiRoute_Returns404InsteadOfTheSpa()
    {
        var client = factory.CreateApiClient();
        await client.LoginAsync(DemoData.Ana);

        var response = await client.GetAsync("/api/does-not-exist", _ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
