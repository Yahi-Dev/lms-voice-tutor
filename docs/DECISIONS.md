# Decisiones y límites

Registro breve de las decisiones de diseño de la prueba de concepto: contexto, decisión, alternativas consideradas y consecuencias.

## 1. Stack: ASP.NET Core 10 + React

**Contexto.** El enunciado deja libre el stack. Hay que entregar en un fin de semana algo que funcione, sea seguro y se pueda explicar en detalle.

**Decisión.** Backend en ASP.NET Core 10 (LTS) con Minimal APIs y EF Core; frontend en React 19 + Vite + TypeScript, con Tailwind y componentes shadcn/ui.

**Alternativas.** Spring Boot + Angular sería el equivalente directo en la plataforma JVM. Next.js full-stack era otra opción, pero mezcla servidor y cliente en un mismo proyecto y complica explicar la frontera de seguridad.

**Consecuencias.** Las piezas tienen equivalente uno a uno en Spring: Identity con cookie ≈ Spring Security con sesión, EF Core ≈ JPA, filtros de endpoint ≈ interceptores, el cliente HTTP tipado ≈ `RestClient`.

## 2. Monolito modular, no capas ni microservicios

**Decisión.** Un único proyecto de servidor organizado por módulos (`Identity`, `Catalog`, `Tutoring`, `Speech`) más `Shared`. Cada módulo registra sus servicios (`AddXModule`) y sus endpoints (`MapX...`). Las interfaces se usan solo donde la implementación cambia: el modelo de lenguaje (`ITutorModel`) y la transcripción (`ISpeechTranscriber`).

**Alternativas.** Clean Architecture en cuatro proyectos con repositorios genéricos y MediatR, o microservicios. Para el alcance de la POC habría sido ceremonia sin beneficio.

**Consecuencias.** Es fácil de leer y de probar. Un módulo puede extraerse a su propio proyecto o servicio porque sus dependencias ya están acotadas.

## 3. SQLite con EF Core y migraciones

**Decisión.** SQLite en un archivo, con migraciones reales que se aplican al iniciar y un seed idempotente que crea el curso y los dos alumnos. Las fechas se guardan en UTC.

**Alternativas.** PostgreSQL desde el inicio (otro contenedor para el evaluador) o `EnsureCreated` (no permite evolucionar el esquema).

**Consecuencias.** Funciona sin instalar nada. Pasar a PostgreSQL es cambiar el proveedor de EF Core (`Npgsql`) y generar sus migraciones. SQLite admite un solo escritor a la vez, suficiente para una demo.

## 4. Sesión con cookie en el mismo origen

**Decisión.** ASP.NET Core Identity con endpoints propios de `login`, `logout` y `me`. La cookie de sesión es `HttpOnly` y `SameSite=Strict`; `Secure` es configurable (`Auth:CookieSecurePolicy`) para que la demo funcione en `http://localhost` y se exija en producción. Hay bloqueo de cuenta tras intentos fallidos y el mismo mensaje, y un tiempo de respuesta similar, para usuario inexistente, contraseña incorrecta o cuenta bloqueada. La SPA se sirve desde el mismo origen que la API.

**Alternativas.** JWT en `localStorage` (expuesto ante XSS) o `MapIdentityApi`, que agrega registro abierto y endpoints que la POC no necesita.

**Consecuencias.** Sin CORS y sin tokens en JavaScript. La protección CSRF combina la cabecera obligatoria `X-CSRF` en peticiones que modifican datos (un sitio ajeno no puede enviarla sin un preflight que la API nunca concede), la verificación de `Sec-Fetch-Site` u `Origin`, y `SameSite=Strict`.

## 5. Control de acceso en un solo lugar

**Decisión.** Todo lo que depende de una lección cuelga del grupo `/api/lessons/{lessonId}`, protegido por un único filtro de matrícula: `404` si la lección no existe y `403` si el alumno no está matriculado. El tutor y la transcripción se montan sobre ese grupo y heredan la comprobación. El id del usuario sale siempre de la sesión: ningún endpoint lo recibe del cliente, y el historial se consulta filtrando por usuario y lección. La política por defecto exige sesión en todo endpoint.

**Consecuencias.** No hay forma de pedir la conversación de otro alumno (IDOR). Las pruebas lo verifican con un tercer alumno matriculado que solo existe en el test. El contenido de una lección sin acceso nunca llega al prompt.

## 6. Tutor anclado a la lección completa, sin RAG

**Decisión.** El prompt contiene las reglas y **toda** la lección, dividida en secciones con id estable. El modelo debe responder con un esquema JSON estricto `{citedSectionIds, status, answer}`, donde las citas solo pueden ser ids de secciones de esa lección. El servidor aplica reglas propias: descarta citas inexistentes, rechaza un `answered` sin evidencia y, tras un reintento, responde honestamente que no hay información suficiente.

**Alternativas.** RAG con embeddings y base vectorial. Con dos lecciones cortas, recuperar fragmentos solo añade puntos de fallo; además, darle al modelo la lección entera da respuestas más fieles y fáciles de probar.

**Consecuencias.** El "no tengo información suficiente" es un campo, no una frase que haya que adivinar. Cuando el contenido crezca (un curso entero, transcripciones de video, PDF), `LessonSection` ya es la unidad que se indexaría (SQLite FTS5 o pgvector) sin cambiar el contrato de salida ni la interfaz. La parte fija del prompt es idéntica entre preguntas para aprovechar la caché de prefijos del proveedor.

Una lección aprendida al probar con el modelo real: si las respuestas anteriores del historial se reenvían como texto plano, el modelo las imita y rompe la salida estructurada (3 de cada 4 preguntas de seguimiento fallaban con `json_validate_failed`). Se reenvían en el mismo JSON del esquema, con sus citas, y con eso el fallo desapareció. Las pruebas automáticas usan un modelo simulado, por eso este tipo de problema solo aparece con una prueba de humo contra el proveedor real.

## 7. Groq como proveedor, detrás de puertos propios

**Decisión.** Groq en plan gratuito (sin tarjeta): `openai/gpt-oss-120b` para el tutor, `openai/gpt-oss-20b` como respaldo cuando el primero está limitado (cada modelo tiene su propia cuota), y `whisper-large-v3-turbo` para la transcripción en español. Se integra con clientes HTTP tipados contra la API compatible con OpenAI, sin SDK. El proveedor se elige por configuración (`Auto`, `Groq` o `Fake`); sin llave, la aplicación arranca en **modo demo** con proveedores simulados deterministas.

**Alternativas.** Los modelos Llama de Groq dejaron de estar disponibles en el plan gratuito en agosto de 2026. Gemini y OpenRouter en plan gratuito tienen límites diarios muy bajos y cláusulas de uso de datos para mejorar sus productos, poco adecuadas para datos de estudiantes. Ollama local exige descargar varios GB y es lento sin GPU.

**Consecuencias.** Cambiar a otro proveedor compatible es cambiar `BaseUrl`, `Model` y la llave. Uno distinto requiere otro adaptador que implemente `ITutorModel`. Las pruebas y el CI no necesitan secretos. La llave vive solo en el servidor (user secrets o `.env`), nunca en el código ni en el navegador.

## 8. Voz: transcripción en el servidor y lectura en el navegador

**Decisión.** El navegador graba con `MediaRecorder` (WebM/Opus, o MP4 en Safari antiguo) y envía el audio como cuerpo crudo. El servidor valida el tipo declarado, los primeros bytes y el tamaño (5 MB), lo transcribe en memoria sin guardarlo y devuelve el texto. El texto queda en el cuadro de pregunta **para que el alumno lo revise antes de enviarlo**. La respuesta se lee con `speechSynthesis` del navegador, eligiendo una voz en español y leyendo por oraciones.

**Alternativas.** Groq no ofrece síntesis de voz en español. Un servidor TTS propio (Piper o Kokoro) agregaría otro contenedor, y la Web Speech API para reconocer voz solo funciona bien en Chrome y envía el audio a terceros.

**Consecuencias.** Se descartan las transcripciones vacías, las de clips silenciosos y las frases que Whisper suele inventar ante el silencio.

Para que la lectura suene humana:
- La aplicación ordena las voces por naturalidad. Primero las neuronales, como las *Online (Natural)* de Edge o las mejoradas de Apple; después las de red, como las de Google; y al final las voces locales clásicas, que suenan robóticas. El alumno puede elegir otra voz en *Voz de lectura*, y la elección se recuerda.
- Antes de leer, el texto se adapta al español hablado: las siglas que se pronuncian como palabra se leen así (*API*, *REST*), las demás se deletrean con el nombre de las letras en español (*HTTP*: "hache te te pe"; *JSON*: "yéison"), las rutas se leen ("barra productos barra 42"), las abreviaturas se expanden y los dos puntos se convierten en pausas.
- La lectura va un poco más lenta que la predeterminada.

La calidad final depende de las voces del navegador. Si no hay voz en español, el botón *Escuchar* se desactiva y lo explica.

## 9. Errores y resiliencia pensados para la interfaz

**Decisión.** Los errores son Problem Details (RFC 9457). Los de negocio y proveedores llevan un `code` estable, y los de proveedores y límites de uso indican además si conviene reintentar (`retryable`); una llave inválida o un modelo inexistente no se ofrecen como reintentables. Los fallos del proveedor se traducen así: límite de cuota → `429` con `Retry-After`; caída → `503`; demora → `504`. Las llamadas salientes pasan por el pipeline de resiliencia de .NET con un solo reintento ante fallos transitorios, nunca ante `429`, porque la cuota gratuita es compartida y reintentar la agota. Hay límite de peticiones por alumno (respuesta `429`, no el `503` por defecto). Cada pregunta lleva un id generado en el cliente que se reutiliza al reenviar el mismo texto, así que repetir nunca duplica un mensaje; si el alumno edita la pregunta, se envía como una pregunta nueva.

**Consecuencias.** La SPA modela la interacción como una máquina de estados con el borrador fuera de cada estado. Ningún fallo (micrófono, red, servicio) borra lo que el alumno escribió o dictó, y siempre hay un mensaje claro con su acción.

## 10. Calidad: pruebas, CI y empaquetado

**Decisión.**
- Pruebas unitarias de las reglas (prompt, validación, adaptadores con un `HttpMessageHandler` simulado).
- Pruebas de integración contra la aplicación real con SQLite y proveedores simulados.
- Pruebas de componentes con Vitest y MSW.
- Pruebas end-to-end con Playwright sobre el build de producción, con la CSP real y micrófono simulado.

GitHub Actions ejecuta todo en cada PR, además de construir y probar la imagen Docker y escanear secretos con gitleaks. El trabajo se integró por fases con PRs y commits pequeños.

**Consecuencias.** Las pruebas verifican comportamiento (estado, citas, códigos de respuesta) y no el texto exacto que genera el modelo, que no es determinista.

## Límites y trabajo futuro

- **Cuota del plan gratuito.** Groq permite unos 8.000 tokens por minuto por modelo para toda la cuenta, unas 2 o 3 preguntas por minuto. Para uso real hace falta un plan de pago o cuotas por institución.
- **Una sola instancia.** SQLite, el limitador de peticiones en memoria y el anillo de llaves en disco asumen un único servidor. Para escalar horizontalmente: PostgreSQL, llaves de Data Protection compartidas (base de datos o Redis) y un limitador distribuido.
- **Sin streaming.** La salida estructurada del proveedor no admite streaming, así que la respuesta llega completa tras un estado *El tutor está pensando*. Con SSE haría falta señalar el estado de otra forma.
- **Voz.** La lectura en voz alta depende de las voces del navegador: Edge da voces neuronales muy naturales, y otros navegadores pueden tener solo voces básicas. Una síntesis neuronal en el servidor (por ejemplo Kokoro o Piper en un contenedor propio) daría la misma voz en todos los navegadores, a costa de otro servicio. Las grabaciones se limitan a 60 segundos.
- **Inyección de prompts.** Está mitigada: el texto del alumno va delimitado, el modelo no tiene herramientas, la salida tiene un esquema fijo y se muestra como texto. Pero no se puede eliminar por completo. El impacto queda acotado a respuestas de menor calidad dentro de la propia sesión del alumno. Un modelo guardián sería el siguiente paso.
- **Fuera de alcance.** Registro de usuarios, recuperación de contraseña, roles de docente o administrador y gestión de contenido. Los saludos se responden con una invitación a preguntar sobre la lección.
- **Contraseñas.** PBKDF2-HMAC-SHA512 con las 100.000 iteraciones por defecto de Identity. OWASP recomienda 220.000 y basta una línea de configuración para subirlo.
- **Llaves en reposo.** El anillo de llaves de Data Protection se guarda sin cifrar en el volumen. En producción convendría cifrarlo con un certificado o un servicio de llaves.

## Relación con la plataforma de inglés con propósitos específicos

El mismo patrón (contexto acotado, contrato de salida estricto y reglas en el servidor) se traslada a lo conversado en la entrevista:

- **Chat restringido a un tema** (por ejemplo, *past participle* con el tema *familia*): una "ficha de tema" ocupa el lugar de la lección en el prompt, y el estado de salida pasa a ser `on_topic` o `redirect`, con el servidor reconduciendo la conversación si se desvía repetidamente.
- **Feedback inmediato en Reading:** el texto del ejercicio dividido en fragmentos citables, como las secciones de esta POC.
- **Reportes de competencias confiables:** las puntuaciones se calculan en código a partir de eventos guardados y con fórmulas versionadas. El modelo solo redacta comentarios sobre números ya calculados, porque las calificaciones generadas por un LLM no son reproducibles y cambian con cada versión del modelo.
