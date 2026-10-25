import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import type { Lesson, TutorExchange } from '@/shared/api/types'

export const apiUrl = (path: string) => new URL(`/api${path}`, window.location.origin).toString()

export const lesson: Lesson = {
  id: 'lesson-1',
  courseId: 'course-1',
  courseTitle: 'APIs REST desde cero',
  slug: 'metodos-http',
  title: 'Métodos HTTP',
  summary: 'Qué hace cada método HTTP.',
  sections: [
    { key: 'get', title: 'GET: leer recursos', body: 'GET sirve para obtener un recurso.' },
    { key: 'post', title: 'POST: crear recursos', body: 'POST sirve para crear un recurso nuevo.' },
  ],
}

export function exchange(overrides: Partial<TutorExchange> = {}): TutorExchange {
  return {
    id: 'exchange-1',
    clientMessageId: 'client-1',
    question: '¿Para qué sirve POST?',
    answer: 'POST sirve para crear un recurso nuevo.',
    status: 'answered',
    inputMode: 'text',
    citations: [{ key: 'post', title: 'POST: crear recursos' }],
    createdAt: '2026-10-25T18:00:00Z',
    ...overrides,
  }
}

export const problem = (status: number, code: string, headers?: Record<string, string>) =>
  HttpResponse.json(
    { status, title: code, code },
    { status, headers: { 'Content-Type': 'application/problem+json', ...headers } },
  )

export const server = setupServer(
  http.get(apiUrl('/lessons/:lessonId/messages'), () => HttpResponse.json([])),
  http.get(apiUrl('/system/status'), () =>
    HttpResponse.json({ tutorProvider: 'demo', speechProvider: 'demo', demoMode: true }),
  ),
)
