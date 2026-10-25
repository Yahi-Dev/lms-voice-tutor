import { useQuery } from '@tanstack/react-query'
import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { TutorPanel } from '@/features/tutor/TutorPanel'
import { ApiError } from '@/shared/api/client'
import { Alert, AlertDescription, AlertTitle } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Skeleton } from '@/shared/ui/skeleton'
import { fetchLesson, lessonQueryKey } from './api'
import { LessonContent } from './LessonContent'
import { sectionElementId } from './sections'

const HIGHLIGHT_MS = 2500

export function LessonPage() {
  const { lessonId = '' } = useParams()
  const lesson = useQuery({
    queryKey: lessonQueryKey(lessonId),
    queryFn: ({ signal }) => fetchLesson(lessonId, signal),
  })
  const [highlightedKey, setHighlightedKey] = useState<string>()

  useEffect(() => {
    if (!highlightedKey) return
    const timer = window.setTimeout(() => setHighlightedKey(undefined), HIGHLIGHT_MS)
    return () => window.clearTimeout(timer)
  }, [highlightedKey])

  const showSection = useCallback((key: string) => {
    const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
    document
      .getElementById(sectionElementId(key))
      ?.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth' })
    setHighlightedKey(key)
  }, [])

  if (lesson.isPending) {
    return (
      <div className="space-y-4" aria-busy="true">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  if (lesson.isError) {
    return <LessonError error={lesson.error} />
  }

  return (
    <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_26rem]">
      <LessonContent lesson={lesson.data} highlightedSectionKey={highlightedKey} />
      <aside className="lg:sticky lg:top-20 lg:h-[calc(100vh-6rem)]">
        <TutorPanel lesson={lesson.data} onShowSection={showSection} />
      </aside>
    </div>
  )
}

function LessonError({ error }: { error: Error }) {
  const kind = error instanceof ApiError ? error.kind : 'unknown'

  const message =
    kind === 'forbidden'
      ? {
          title: 'No tienes acceso a esta lección',
          body: 'Esta lección pertenece a un curso en el que no estás matriculado.',
        }
      : kind === 'not_found'
        ? {
            title: 'La lección no existe',
            body: 'Puede que el enlace esté incompleto o que la lección se haya retirado.',
          }
        : {
            title: 'No se pudo cargar la lección',
            body: 'Revisa tu conexión e inténtalo de nuevo.',
          }

  return (
    <div className="mx-auto max-w-xl space-y-4">
      <Alert variant={kind === 'forbidden' ? 'default' : 'destructive'}>
        <AlertTitle>
          <h1 className="font-semibold">{message.title}</h1>
        </AlertTitle>
        <AlertDescription>{message.body}</AlertDescription>
      </Alert>
      <Button asChild variant="outline">
        <Link to="/">Volver a mis cursos</Link>
      </Button>
    </div>
  )
}
