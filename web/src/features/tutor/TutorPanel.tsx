import { AlertCircle, RotateCcw } from 'lucide-react'
import type { Lesson } from '@/shared/api/types'
import { useOnlineStatus } from '@/shared/hooks/useOnlineStatus'
import { Alert, AlertDescription } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card'
import { Skeleton } from '@/shared/ui/skeleton'
import { Composer } from './Composer'
import { MessageList } from './MessageList'
import type { TutorPhase } from './tutorReducer'
import { useSpeech } from './useSpeech'
import { useTutorSession } from './useTutorSession'
import { VoicePicker } from './VoicePicker'

interface TutorPanelProps {
  lesson: Lesson
  onShowSection: (key: string) => void
}

export function TutorPanel({ lesson, onShowSection }: TutorPanelProps) {
  const session = useTutorSession(lesson.id)
  const speech = useSpeech()
  const online = useOnlineStatus()
  const { phase } = session.state

  const pendingQuestion =
    phase.name === 'sending'
      ? { question: phase.pending, failed: false }
      : phase.name === 'error' && phase.pending
        ? { question: phase.pending, failed: true }
        : undefined

  return (
    <Card className="flex h-full max-h-[80vh] flex-col lg:max-h-none">
      <CardHeader>
        <CardTitle>
          <h2 className="text-lg font-semibold">Pregúntale al tutor</h2>
        </CardTitle>
        <CardDescription>Responde solo con el contenido de esta lección.</CardDescription>
        {speech.availability === 'ready' && (
          <VoicePicker
            voices={speech.voices}
            voice={speech.voice}
            hasNaturalVoice={speech.hasNaturalVoice}
            onSelect={speech.selectVoice}
          />
        )}
      </CardHeader>

      <CardContent className="flex min-h-0 flex-1 flex-col gap-3">
        <div className="min-h-40 flex-1 overflow-y-auto pr-1">
          {session.messages.isPending ? (
            <div className="space-y-3" aria-busy="true">
              <Skeleton className="h-12 w-3/4" />
              <Skeleton className="ml-auto h-10 w-2/3" />
            </div>
          ) : session.messages.isError ? (
            <Alert variant="destructive">
              <AlertDescription>
                No se pudo cargar tu conversación anterior.{' '}
                <Button variant="link" size="xs" onClick={() => session.messages.refetch()}>
                  Reintentar
                </Button>
              </AlertDescription>
            </Alert>
          ) : (
            <MessageList
              exchanges={session.messages.data}
              suggestedTopic={(lesson.sections[1] ?? lesson.sections[0])?.title}
              pending={pendingQuestion}
              speech={speech}
              onShowSection={onShowSection}
            />
          )}
        </div>

        <p role="status" aria-live="polite" className="min-h-5 text-sm text-muted-foreground">
          {statusText(phase)}
        </p>

        {phase.name === 'error' && (
          <Alert variant="destructive">
            <AlertCircle aria-hidden="true" />
            <AlertDescription className="space-y-2">
              <p>{phase.error.message}</p>
              <div className="flex gap-2">
                {phase.error.retryable && (
                  <Button size="sm" variant="outline" onClick={session.retry} disabled={!online}>
                    <RotateCcw aria-hidden="true" />
                    Reintentar
                  </Button>
                )}
                <Button size="sm" variant="ghost" onClick={session.dismissError}>
                  Cerrar
                </Button>
              </div>
            </AlertDescription>
          </Alert>
        )}

        <Composer
          state={session.state}
          online={online}
          isRecording={session.recorder.isRecording}
          elapsedMs={session.recorder.elapsedMs}
          onDraftChange={session.setDraft}
          onSubmit={session.submit}
          onStartRecording={() => void session.recorder.start()}
          onStopRecording={session.recorder.stop}
          onCancelVoice={session.cancelVoice}
        />
      </CardContent>
    </Card>
  )
}

function statusText(phase: TutorPhase): string {
  switch (phase.name) {
    case 'recording':
      return 'Grabando tu pregunta…'
    case 'transcribing':
      return 'Transcribiendo tu pregunta…'
    case 'sending':
      return 'El tutor está pensando…'
    default:
      return ''
  }
}
