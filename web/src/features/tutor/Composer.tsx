import { Mic, Send, Square, X } from 'lucide-react'
import { type KeyboardEvent, useEffect, useRef } from 'react'
import { formatElapsed } from '@/shared/lib/time'
import { isRecordingSupported } from '@/shared/media/recording'
import { Button } from '@/shared/ui/button'
import { Textarea } from '@/shared/ui/textarea'
import { MAX_QUESTION_LENGTH, type TutorState } from './tutorReducer'

interface ComposerProps {
  state: TutorState
  online: boolean
  isRecording: boolean
  elapsedMs: number
  onDraftChange: (text: string) => void
  onSubmit: () => void
  onStartRecording: () => void
  onStopRecording: () => void
  onCancelVoice: () => void
}

export function Composer({
  state,
  online,
  isRecording,
  elapsedMs,
  onDraftChange,
  onSubmit,
  onStartRecording,
  onStopRecording,
  onCancelVoice,
}: ComposerProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const phase = state.phase.name
  const busy = phase === 'sending' || phase === 'transcribing'
  const canRecord = isRecordingSupported()
  const canSend = online && !busy && !isRecording && state.draft.trim().length >= 2

  // After a transcription the student reviews the text before sending it.
  useEffect(() => {
    if (state.transcriptReady) textareaRef.current?.focus()
  }, [state.transcriptReady])

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault()
      if (canSend) onSubmit()
    }
  }

  return (
    <form
      className="space-y-2"
      onSubmit={(event) => {
        event.preventDefault()
        if (canSend) onSubmit()
      }}
      onKeyDown={(event) => {
        if (event.key === 'Escape' && (isRecording || phase === 'transcribing')) onCancelVoice()
      }}
    >
      <label htmlFor="tutor-question" className="sr-only">
        Tu pregunta
      </label>
      <Textarea
        id="tutor-question"
        ref={textareaRef}
        rows={3}
        maxLength={MAX_QUESTION_LENGTH}
        placeholder="Escribe tu pregunta sobre la lección o grábala con el micrófono…"
        value={state.draft}
        disabled={phase === 'sending'}
        onChange={(event) => onDraftChange(event.target.value)}
        onKeyDown={handleKeyDown}
        aria-describedby="tutor-question-hint"
      />
      <p id="tutor-question-hint" className="text-xs text-muted-foreground">
        {state.transcriptReady
          ? 'Revisa la transcripción y corrígela si hace falta antes de enviarla.'
          : `Enter para enviar, Shift+Enter para una línea nueva. ${state.draft.length}/${MAX_QUESTION_LENGTH}`}
      </p>

      <div className="flex flex-wrap items-center gap-2">
        {isRecording ? (
          <>
            <Button type="button" variant="destructive" onClick={onStopRecording}>
              <Square aria-hidden="true" />
              Detener grabación
            </Button>
            <span className="flex items-center gap-1.5 text-sm tabular-nums" aria-hidden="true">
              <span className="size-2 rounded-full bg-red-500 motion-safe:animate-pulse" />
              {formatElapsed(elapsedMs)}
            </span>
            <Button type="button" variant="ghost" size="sm" onClick={onCancelVoice}>
              <X aria-hidden="true" />
              Cancelar
            </Button>
          </>
        ) : (
          <Button
            type="button"
            variant="outline"
            onClick={onStartRecording}
            disabled={!canRecord || busy}
            title={canRecord ? undefined : 'Este navegador no permite grabar en esta página'}
          >
            <Mic aria-hidden="true" />
            Grabar pregunta
          </Button>
        )}

        {phase === 'transcribing' && (
          <Button type="button" variant="ghost" size="sm" onClick={onCancelVoice}>
            Cancelar transcripción
          </Button>
        )}

        <Button type="submit" className="ml-auto" disabled={!canSend}>
          <Send aria-hidden="true" />
          Enviar
        </Button>
      </div>
    </form>
  )
}
