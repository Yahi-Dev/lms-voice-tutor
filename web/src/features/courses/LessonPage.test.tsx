import { screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { renderWithProviders } from '@/test/render'
import { apiUrl, lesson, problem, server } from '@/test/server'
import { LessonPage } from './LessonPage'

const renderLesson = () =>
  renderWithProviders(<LessonPage />, { route: '/lessons/lesson-1', path: '/lessons/:lessonId' })

describe('LessonPage', () => {
  it('shows the lesson next to the tutor', async () => {
    server.use(http.get(apiUrl('/lessons/:lessonId'), () => HttpResponse.json(lesson)))

    renderLesson()

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Métodos HTTP' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'POST: crear recursos' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Pregúntale al tutor' })).toBeInTheDocument()
  })

  it('tells a student without access that the lesson is not available to them', async () => {
    server.use(http.get(apiUrl('/lessons/:lessonId'), () => problem(403, 'lesson_forbidden')))

    renderLesson()

    expect(await screen.findByText('No tienes acceso a esta lección')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Pregúntale al tutor' })).not.toBeInTheDocument()
  })
})
