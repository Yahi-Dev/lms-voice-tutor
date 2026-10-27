# Tutor virtual con voz para un LMS

[![CI](https://github.com/Yahi-Dev/lms-voice-tutor/actions/workflows/ci.yml/badge.svg)](https://github.com/Yahi-Dev/lms-voice-tutor/actions/workflows/ci.yml)

Prueba de concepto: un alumno abre una lección, pregunta por texto o por voz y recibe una respuesta basada **solo** en el contenido de esa lección, que también puede escuchar. Si la lección no cubre la pregunta, el tutor lo dice. La conversación se guarda por alumno y lección, y una cuenta sin acceso no puede ver la lección ni los mensajes de otros.

![Lección con el panel del tutor](docs/images/leccion.png)

## Qué cubre

| # | Requisito del enunciado | Cómo se resuelve |
|---|---|---|
| 1 | Un curso y dos lecciones | Curso *APIs REST desde cero* con *Métodos HTTP* y *Códigos de estado HTTP*, cargado al iniciar. |
| 2 | Un alumno con acceso y otro sin él | **Ana** está matriculada; **Bruno** no. El acceso se comprueba en el servidor en cada petición. |
| 3 | Tutor contextual que avisa si no tiene información | El modelo recibe la lección completa y responde con un esquema JSON estricto: `answered` (con las secciones citadas) o `insufficient_information`. |
| 4 | Preguntas por texto o voz; respuesta audible | La voz se graba en el navegador, se transcribe en el servidor (Whisper) y queda en el cuadro de texto para revisarla antes de enviarla. La respuesta se lee con la síntesis de voz del navegador. |
| 5 | Historial recuperable | Cada intercambio se guarda por alumno y lección; al volver, la conversación aparece de nuevo. |
| 6 | Estado visible y recuperación ante fallos | Estados *Grabando / Transcribiendo / El tutor está pensando*. Mensajes claros y reintento para micrófono bloqueado, sin conexión, servicio caído o saturado; la pregunta nunca se pierde. |

## Inicio rápido

### Opción A: Docker (recomendada)

Requisitos: Docker Desktop.

```bash
cp .env.example .env        # pega tu llave de Groq en Tutoring__ApiKey (sin ella arranca en modo demo)
docker compose up --build
```

Abre **<http://localhost:8080>** (el micrófono solo funciona en `localhost` o con HTTPS, no con la IP de la red local).

### Opción B: ejecución local

Requisitos: .NET SDK 10.0.4xx y Node.js 24.

```bash
# Backend: http://localhost:5080 (crea la base SQLite y los datos de prueba al arrancar)
dotnet run --project src/LmsTutor.Server

# Frontend, en otra terminal: http://localhost:5173 (redirige /api al backend)
cd web
npm ci
npm run dev
```

Para usar Groq en local, guarda la llave con *user secrets*. Así nunca termina en el repositorio:

```bash
dotnet user-secrets set "Tutoring:ApiKey" "gsk_..." --project src/LmsTutor.Server
```

## Modo demo y proveedor real

> Para evaluar el tutor con un modelo de lenguaje y la transcripción reales hace falta una llave **gratuita** de Groq; se crea en un par de minutos y no pide tarjeta. Sin llave la aplicación funciona de punta a punta con proveedores simulados, útil para revisar el flujo, la interfaz y los controles de acceso.

| | Sin llave (modo demo) | Con llave de Groq (gratuita, sin tarjeta) |
|---|---|---|
| Tutor | Modelo de demostración que responde citando la sección con más coincidencias, o informa que la lección no cubre la pregunta | `openai/gpt-oss-120b` con salida estructurada; `openai/gpt-oss-20b` como respaldo si el primero está limitado |
| Transcripción | Devuelve una frase fija (*«¿Para qué sirve POST?»*) tras una grabación real | `whisper-large-v3-turbo` en español |
| Interfaz | Muestra un aviso de *Modo demo* | Sin aviso |

La llave se crea en <https://console.groq.com/keys>. Basta con una para el tutor y la transcripción. En la configuración se llama `Tutoring__ApiKey` (variable de entorno) o `Tutoring:ApiKey` (user secrets).

## Cuentas de prueba

| Alumno | Correo | Contraseña | Acceso |
|---|---|---|---|
| Ana Gómez | `ana@demo.local` | `Ana-Demo-2026!` | Matriculada en el curso |
| Bruno Pérez | `bruno@demo.local` | `Bruno-Demo-2026!` | Sin acceso |

Son datos de evaluación locales, no secretos. La pantalla de inicio de sesión tiene botones para rellenarlas.

## Cómo verificar

| # | Qué probar | Pasos | Resultado esperado | Prueba automática |
|---|---|---|---|---|
| 1 | Contenido | Entra como Ana | El curso con sus dos lecciones | `CatalogAccessTests` |
| 2 | Alumno sin acceso | Entra como Bruno; luego pega la URL de una lección de Ana | *Aún no tienes cursos asignados* y *No tienes acceso a esta lección* | `CatalogAccessTests`, e2e |
| 3 | Tutor contextual | En *Métodos HTTP* pregunta «¿Para qué sirve POST?» y luego «¿Cómo despliego en Kubernetes?» | Respuesta con la fuente *POST: crear recursos*; después, *La lección no cubre esta pregunta* | `TutorTests`, e2e |
| 4 | Voz | Pulsa *Grabar pregunta*, habla, *Detener grabación*; revisa el texto y *Enviar*; pulsa *Escuchar* | La transcripción aparece editable; la respuesta se lee en voz alta | `TutorPanel.test.tsx`, e2e |
| 5 | Historial | Recarga la página o cierra sesión y vuelve a entrar | La conversación sigue ahí | `TutorTests`, e2e |
| 6 | Recuperación | a) Bloquea el micrófono del sitio y pulsa *Grabar pregunta*. b) Activa *Offline* en DevTools. c) En modo demo, agrega `Tutoring__FakeFailure=Unavailable` al `.env` y reinicia | a) Explica cómo activarlo y deja seguir por texto. b) Aviso *Sin conexión*, la pregunta se conserva y se envía al volver la red. c) *El tutor no está disponible*, la pregunta se conserva y aparece *Reintentar* | `TutorPanel.test.tsx`, `TutorTests`, e2e |

Controles de seguridad:

| Control | Cómo comprobarlo | Esperado |
|---|---|---|
| IDOR / acceso | Con la sesión de Bruno: `GET /api/lessons/{id}/messages` de una lección de Ana | `403`; ningún endpoint acepta un id de usuario |
| XSS | Pregunta `<img src=x onerror=alert(1)>` | Se muestra como texto, no se ejecuta |
| Inyección SQL | Inicia sesión con un correo válido y la contraseña `' OR 1=1--` | `401`: EF Core solo usa consultas parametrizadas (en el campo de correo responde `400` por validación) |
| CSRF | `POST` a la API sin la cabecera `X-CSRF` | `403` |
| Abuso | Más de 10 preguntas en un minuto | `429` con `Retry-After` |
| Secretos | `git log -p \| grep -E "gsk_[A-Za-z0-9]{20,}"` | Sin resultados; el CI ejecuta gitleaks sobre todo el historial |

## Pruebas y CI

```bash
dotnet test --solution LmsTutor.slnx        # 115 pruebas: unitarias e integración (API real + SQLite)
cd web
npm test                                     # 47 pruebas de componentes y lógica (Vitest)

# End-to-end: 5 pruebas en Chromium con micrófono simulado contra el build de producción.
# Requieren el .NET SDK (arrancan el backend en el puerto 5090) y descargar Chromium una vez.
npx playwright install chromium
npm run build && npm run test:e2e
```

GitHub Actions ejecuta en cada PR: formato, compilación con warnings como errores, pruebas, auditoría de dependencias, lint y tipos del frontend, pruebas end-to-end, construcción y prueba de humo de la imagen Docker, y escaneo de secretos.

## Arquitectura

```mermaid
flowchart LR
  subgraph Navegador
    SPA["SPA React<br/>lección · tutor · grabación"]
    TTS["speechSynthesis<br/>(lectura en voz alta)"]
  end

  subgraph API["ASP.NET Core 10 · mismo origen"]
    MW["Cookie de sesión · CSRF · rate limit · CSP"]
    ACL["Política de matrícula<br/>/api/lessons/{id}/*"]
    TUT["Tutoring<br/>PromptBuilder · validación"]
    SPE["Speech<br/>validación de audio"]
  end

  DB[("SQLite<br/>EF Core")]
  GROQ["Groq<br/>gpt-oss · Whisper"]

  SPA -->|fetch /api| MW --> ACL
  ACL --> TUT
  ACL --> SPE
  TUT --> DB
  ACL --> DB
  TUT -->|ITutorModel| GROQ
  SPE -->|ISpeechTranscriber| GROQ
  SPA --> TTS
```

```
src/LmsTutor.Server/
  Modules/            Identity · Catalog (cursos, lecciones, matrícula) · Tutoring · Speech
    */Providers/      adaptadores Groq (OpenAI-compatible) y de demostración
  Shared/             persistencia, errores, CSRF, cabeceras, rate limiting
web/src/
  features/           auth · courses · tutor (máquina de estados, grabación, voz)
  shared/             cliente de API, utilidades de medios, componentes UI
tests/                pruebas unitarias e integración (.NET)
web/e2e/              pruebas end-to-end (Playwright)
```

Las decisiones, alternativas y límites están en **[docs/DECISIONS.md](docs/DECISIONS.md)**.

## Solución de problemas

- **El micrófono no funciona:** usa `http://localhost`, no una IP de red; revisa el permiso en el icono junto a la dirección del sitio.
- **La voz suena robótica:** abre la aplicación en **Microsoft Edge**, que trae voces neuronales en español (por ejemplo *Salome* o *Gonzalo* de Colombia, *Dalia* o *Jorge* de México). La aplicación elige la más natural disponible y puedes cambiarla en *Voz de lectura*. Chrome ofrece las voces *Google español*.
- **"Escuchar" está desactivado:** el navegador no tiene ninguna voz en español. En Linux puede faltar.
- **`429` del tutor con Groq:** el plan gratuito permite unas pocas preguntas por minuto para toda la cuenta (cada pregunta usa unos 1.300 tokens). Espera los segundos indicados o usa el modo demo.
- **Ana o Bruno no pueden entrar:** tras 5 contraseñas incorrectas la cuenta se bloquea 2 minutos (y responde igual que una contraseña incorrecta). Espera o reinicia la aplicación, que desbloquea las cuentas de prueba al arrancar.
- **Puerto ocupado:** cambia `APP_PORT` en `.env`.
- **Empezar de cero:** `docker compose down -v` borra la base de datos y las sesiones.
