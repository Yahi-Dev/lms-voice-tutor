using LmsTutor.Server.Modules.Tutoring;

namespace LmsTutor.UnitTests.Tutoring;

internal static class TestLessons
{
    public static LessonContext HttpMethods { get; } = new(
        Guid.Parse("01a1744c-6faf-7f9c-a0e6-e71f4855324f"),
        "metodos-http",
        "Métodos HTTP",
        [
            new("get", "GET: leer recursos", "GET sirve para obtener la representación de un recurso. No modifica datos."),
            new("post", "POST: crear recursos", "POST sirve para enviar datos al servidor y crear un recurso nuevo. Suele responder 201 Created."),
            new("delete", "DELETE: eliminar recursos", "DELETE elimina el recurso indicado. Es idempotente."),
        ]);
}
