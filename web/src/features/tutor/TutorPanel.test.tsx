import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { deniedMicrophone, installSpeechSynthesis, workingMicrophone } from '@/test/media'
import { renderWithProviders } from '@/test/render'
import { apiUrl, exchange, lesson, problem, server } from '@/test/server'
import { TutorPanel } from './TutorPanel'

const renderPanel = (onShowSection = vi.fn()) =>
  renderWithProviders(<TutorPanel lesson={lesson} onShowSection={onShowSection} />, {
    route: '/lessons/lesson-1',
    path: '/lessons/:lessonId',
  })

const questionBox = () => screen.getByRole('textbox', { name: 'Tu pregunta' })

describe('TutorPanel', () => {
  it('answers a typed question with its sources and lets the student listen to it', async () => {
    const spoken = installSpeechSynthesis()
    server.use(
      http.post(apiUrl('/lessons/:lessonId/messages'), () => HttpResponse.json(exchange())),
    )
    const onShowSection = vi.fn()
    const user = userEvent.setup()
    renderPanel(onShowSection)

    await user.type(questionBox(), '¿Para qué sirve POST?')
    await user.click(screen.getByRole('button', { name: 'Enviar' }))

    expect(await screen.findByText('POST sirve para crear un recurso nuevo.')).toBeInTheDocument()
    expect(questionBox()).toHaveValue('')

    await user.click(screen.getByRole('button', { name: /Ver la sección «POST: crear recursos»/ }))
    expect(onShowSection).toHaveBeenCalledWith('post')

    await user.click(screen.getByRole('button', { name: 'Escuchar' }))
    expect(spoken).toEqual(['POST sirve para crear un recurso nuevo.'])
  })

  it('shows a neutral notice when the lesson does not cover the question', async () => {
    server.use(
      http.post(apiUrl('/lessons/:lessonId/messages'), () =>
        HttpResponse.json(
          exchange({
            status: 'insufficient_information',
            answer: 'Esta lección no cubre esa pregunta.',
            citations: [],
          }),
        ),
      ),
    )
    const user = userEvent.setup()
    renderPanel()

    await user.type(questionBox(), '¿Quién ganó el Mundial?{Enter}')

    expect(await screen.findByText('La lección no cubre esta pregunta')).toBeInTheDocument()
  })

  it('keeps the question after a provider outage and retries with the same message id', async () => {
    const clientIds: string[] = []
    let attempts = 0
    server.use(
      http.post(apiUrl('/lessons/:lessonId/messages'), async ({ request }) => {
        const body = (await request.json()) as { clientMessageId: string }
        clientIds.push(body.clientMessageId)
        attempts += 1
        return attempts === 1
          ? problem(503, 'tutor_unavailable', { 'Retry-After': '10' })
          : HttpResponse.json(exchange())
      }),
    )
    const user = userEvent.setup()
    renderPanel()

    await user.type(questionBox(), '¿Para qué sirve POST?')
    await user.click(screen.getByRole('button', { name: 'Enviar' }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('El tutor no está disponible')
    expect(questionBox()).toHaveValue('¿Para qué sirve POST?')
    expect(screen.getByText('No enviada')).toBeInTheDocument()

    await user.click(within(alert).getByRole('button', { name: 'Reintentar' }))

    expect(await screen.findByText('POST sirve para crear un recurso nuevo.')).toBeInTheDocument()
    expect(clientIds).toHaveLength(2)
    expect(clientIds[0]).toBe(clientIds[1])
  })

  it('renders markup in an answer as plain text', async () => {
    server.use(
      http.get(apiUrl('/lessons/:lessonId/messages'), () =>
        HttpResponse.json([exchange({ answer: '<img src=x onerror="alert(1)">' })]),
      ),
    )
    renderPanel()

    expect(await screen.findByText('<img src=x onerror="alert(1)">')).toBeInTheDocument()
    expect(document.querySelector('img')).toBeNull()
  })

  it('restores the previous conversation of the lesson', async () => {
    server.use(
      http.get(apiUrl('/lessons/:lessonId/messages'), () =>
        HttpResponse.json([exchange({ question: '¿Qué hace GET?', answer: 'GET lee recursos.' })]),
      ),
    )
    renderPanel()

    expect(await screen.findByText('¿Qué hace GET?')).toBeInTheDocument()
    expect(screen.getByText('GET lee recursos.')).toBeInTheDocument()
  })

  it('explains a blocked microphone and still lets the student type', async () => {
    deniedMicrophone()
    const user = userEvent.setup()
    renderPanel()

    await user.click(screen.getByRole('button', { name: 'Grabar pregunta' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'El permiso del micrófono está bloqueado',
    )
    await user.type(questionBox(), 'Pregunta escrita')
    expect(screen.getByRole('button', { name: 'Enviar' })).toBeEnabled()
  })

  it('transcribes a recording into the question box for review before sending', async () => {
    const track = workingMicrophone()
    const sent: unknown[] = []
    server.use(
      http.post(apiUrl('/lessons/:lessonId/transcriptions'), () =>
        HttpResponse.json({ text: '¿Para qué sirve POST?' }),
      ),
      http.post(apiUrl('/lessons/:lessonId/messages'), async ({ request }) => {
        sent.push(await request.json())
        return HttpResponse.json(exchange({ inputMode: 'voice' }))
      }),
    )
    vi.useFakeTimers({ toFake: ['Date'] })
    const user = userEvent.setup()
    renderPanel()

    await user.click(screen.getByRole('button', { name: 'Grabar pregunta' }))
    expect(await screen.findByText('Grabando tu pregunta…')).toBeInTheDocument()
    vi.setSystemTime(Date.now() + 2_000)
    await user.click(screen.getByRole('button', { name: 'Detener grabación' }))

    await waitFor(() => expect(questionBox()).toHaveValue('¿Para qué sirve POST?'))
    expect(track.stop).toHaveBeenCalled()
    expect(sent).toHaveLength(0)

    await user.click(screen.getByRole('button', { name: 'Enviar' }))
    await waitFor(() => expect(sent).toEqual([expect.objectContaining({ inputMode: 'voice' })]))
    vi.useRealTimers()
  })
})
