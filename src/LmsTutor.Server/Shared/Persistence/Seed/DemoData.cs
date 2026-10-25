using LmsTutor.Server.Modules.Catalog;

namespace LmsTutor.Server.Shared.Persistence.Seed;

/// <summary>
/// Sample course and evaluation accounts. The passwords are local fixtures documented in the
/// README, not secrets, and they are only seeded when <c>Seed:DemoData</c> is enabled.
/// </summary>
public static class DemoData
{
    public sealed record DemoStudent(string Email, string Password, string DisplayName, bool EnrolledInCourse);

    public const string CourseSlug = "apis-rest";

    public static readonly DemoStudent Ana = new("ana@demo.local", "Ana-Demo-2026!", "Ana Gómez", EnrolledInCourse: true);
    public static readonly DemoStudent Bruno = new("bruno@demo.local", "Bruno-Demo-2026!", "Bruno Pérez", EnrolledInCourse: false);

    public static IReadOnlyList<DemoStudent> Students { get; } = [Ana, Bruno];

    public static Course CreateCourse()
    {
        var course = new Course
        {
            Slug = CourseSlug,
            Title = "APIs REST desde cero",
            Description = "Fundamentos para diseñar y consumir APIs HTTP: métodos, códigos de estado y buenas prácticas.",
        };

        course.Lessons.Add(CreateLesson(course.Id, 1, "metodos-http", "Métodos HTTP",
            "Qué hace cada método HTTP y cuándo usarlo al diseñar una API.",
            ("que-es-un-metodo", "¿Qué es un método HTTP?",
                "Cada petición HTTP indica un método (también llamado verbo) que expresa la intención del cliente sobre un recurso. " +
                "El recurso se identifica con una URL, por ejemplo /productos/42, y el método dice qué se quiere hacer con él: leerlo, " +
                "crearlo, modificarlo o eliminarlo. Usar el método correcto hace que la API sea predecible y que intermediarios como " +
                "cachés y proxies sepan cómo tratar cada petición."),
            ("get", "GET: leer recursos",
                "GET sirve para obtener la representación de un recurso o de una colección, por ejemplo GET /productos o " +
                "GET /productos/42. No debe modificar datos en el servidor y no lleva cuerpo en la petición; los filtros viajan en la " +
                "URL como parámetros de consulta (GET /productos?categoria=libros). Como no cambia nada, sus respuestas pueden " +
                "guardarse en caché."),
            ("post", "POST: crear recursos",
                "POST sirve para enviar datos al servidor y crear un recurso nuevo dentro de una colección. Por ejemplo, POST /productos " +
                "con un cuerpo JSON que describe el producto. Si la creación funciona, el servidor responde normalmente 201 Created e " +
                "incluye la cabecera Location con la URL del recurso creado. También se usa POST para acciones que no encajan en otro " +
                "método, como procesar un pago. POST no es idempotente: repetir la misma petición puede crear dos recursos distintos."),
            ("put-patch", "PUT y PATCH: actualizar recursos",
                "PUT reemplaza por completo un recurso existente con la representación enviada: PUT /productos/42 con todos sus campos. " +
                "PATCH aplica una modificación parcial y solo envía los campos que cambian, por ejemplo el precio. PUT es idempotente: " +
                "enviar la misma petición varias veces deja el recurso en el mismo estado. Un PATCH puede no serlo, según cómo se " +
                "defina el cambio."),
            ("delete", "DELETE: eliminar recursos",
                "DELETE elimina el recurso indicado, por ejemplo DELETE /productos/42. Suele responder 204 No Content cuando se borra " +
                "correctamente, o 404 Not Found si el recurso no existe. Es idempotente: después de la primera eliminación, repetir la " +
                "petición no cambia nada más en el servidor."),
            ("idempotencia", "Métodos seguros e idempotentes",
                "Un método es seguro si no modifica el estado del servidor: GET y HEAD lo son. Un método es idempotente si hacer la " +
                "misma petición una o varias veces produce el mismo efecto: GET, PUT y DELETE lo son, POST no. Esta propiedad importa " +
                "cuando falla la red: un cliente puede reintentar con tranquilidad una petición idempotente, pero reintentar un POST " +
                "puede duplicar datos, por eso muchas APIs aceptan una clave de idempotencia en esas peticiones.")));

        course.Lessons.Add(CreateLesson(course.Id, 2, "codigos-de-estado", "Códigos de estado HTTP",
            "Cómo interpretar y elegir los códigos de estado de una respuesta.",
            ("familias", "Las cinco familias de códigos",
                "Toda respuesta HTTP incluye un código de estado de tres dígitos. El primer dígito indica la familia: 1xx son " +
                "respuestas informativas, 2xx indican éxito, 3xx redirecciones, 4xx errores causados por la petición del cliente y " +
                "5xx errores del servidor. Leer primero la familia ayuda a saber de inmediato si la petición funcionó y de quién es " +
                "la responsabilidad."),
            ("exito", "2xx: la petición funcionó",
                "200 OK es la respuesta exitosa general, por ejemplo al leer un recurso con GET. 201 Created indica que se creó un " +
                "recurso nuevo, típicamente después de un POST, y suele ir acompañado de la cabecera Location. 204 No Content indica " +
                "éxito sin cuerpo en la respuesta, algo común al eliminar con DELETE o al actualizar sin devolver datos."),
            ("errores-cliente", "4xx: errores del cliente",
                "400 Bad Request indica que la petición está mal formada o tiene datos inválidos. 401 Unauthorized significa que falta " +
                "autenticación o que no es válida, mientras que 403 Forbidden significa que el usuario está autenticado pero no tiene " +
                "permiso. 404 Not Found indica que el recurso no existe. 409 Conflict señala un conflicto con el estado actual, como " +
                "un registro duplicado, y 429 Too Many Requests indica que el cliente superó el límite de peticiones permitido."),
            ("errores-servidor", "5xx: errores del servidor",
                "500 Internal Server Error indica un fallo inesperado en el servidor. 502 Bad Gateway y 504 Gateway Timeout aparecen " +
                "cuando un servidor intermedio no recibe una respuesta válida o a tiempo de otro servicio. 503 Service Unavailable " +
                "indica que el servicio no puede atender por ahora, por mantenimiento o sobrecarga, y puede incluir la cabecera " +
                "Retry-After con el tiempo de espera sugerido antes de reintentar."),
            ("buenas-practicas", "Elegir el código correcto",
                "Una buena API usa el código más específico posible y acompaña los errores con un cuerpo que explique el problema, " +
                "por ejemplo con el formato estándar Problem Details (RFC 9457), que incluye un título, un detalle y el código de " +
                "estado. Nunca se debe responder 200 con un mensaje de error dentro del cuerpo, porque los clientes y las " +
                "herramientas de monitoreo dependen del código para saber qué ocurrió.")));

        return course;
    }

    private static Lesson CreateLesson(
        Guid courseId, int position, string slug, string title, string summary,
        params (string Key, string Title, string Body)[] sections)
    {
        var lesson = new Lesson { CourseId = courseId, Position = position, Slug = slug, Title = title, Summary = summary };

        for (var i = 0; i < sections.Length; i++)
        {
            var (key, sectionTitle, body) = sections[i];
            lesson.Sections.Add(new LessonSection { LessonId = lesson.Id, Position = i + 1, Key = key, Title = sectionTitle, Body = body });
        }

        return lesson;
    }
}
