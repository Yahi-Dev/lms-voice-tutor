import { Info, Mic, Square, Volume2 } from 'lucide-react'
import { useEffect, useRef } from 'react'
import type { TutorExchange } from '@/shared/api/types'
import { cn } from '@/shared/lib/utils'
import { Button } from '@/shared/ui/button'
import type { PendingQuestion } from './tutorReducer'
import type { SpeechAvailability } from './useSpeech'

interface MessageListProps {
  exchanges: TutorExchange[]
  pending?: { question: PendingQuestion; failed: boolean }
  speech: {
    availability: SpeechAvailability
    speakingId: string | null
    speak: (id: string, text: string) => void
    stop: () => void
  }
  onShowSection: (key: string) => void
  /** A topic of the current lesson to suggest when the conversation is empty. */
  suggestedTopic?: string
}

export function MessageList({
  exchanges,
  pending,
  speech,
  onShowSection,
  suggestedTopic,
}: MessageListProps) {
  const endRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
    endRef.current?.scrollIntoView?.({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'end' })
  }, [exchanges.length, pending?.question.clientMessageId])

  if (exchanges.length === 0 && !pending) {
    return (
      <p className="px-1 py-6 text-center text-sm text-muted-foreground">
        Aún no hay preguntas en esta lección.
        {suggestedTopic &&
          ` Pregunta sobre cualquiera de sus temas, por ejemplo «${suggestedTopic}».`}
      </p>
    )
  }

  return (
    <>
      <ol className="space-y-4" aria-label="Conversación con el tutor">
        {exchanges.map((exchange) => (
          <li key={exchange.id} className="space-y-2">
            <QuestionBubble text={exchange.question} viaVoice={exchange.inputMode === 'voice'} />
            <AnswerBubble exchange={exchange} speech={speech} onShowSection={onShowSection} />
          </li>
        ))}
        {pending && (
          <li className="space-y-1">
            <QuestionBubble
              text={pending.question.text}
              viaVoice={pending.question.inputMode === 'voice'}
            />
            <p
              className={cn(
                'text-right text-xs',
                pending.failed ? 'text-destructive' : 'text-muted-foreground',
              )}
            >
              {pending.failed ? 'No enviada' : 'Enviando…'}
            </p>
          </li>
        )}
      </ol>
      <div ref={endRef} />
    </>
  )
}

function QuestionBubble({ text, viaVoice }: { text: string; viaVoice: boolean }) {
  return (
    <div className="ml-8 flex justify-end">
      <p className="rounded-2xl rounded-br-sm bg-primary px-3 py-2 text-sm whitespace-pre-wrap text-primary-foreground">
        {viaVoice && (
          <>
            <Mic className="mr-1 inline size-3.5 align-[-2px]" aria-hidden="true" />
            <span className="sr-only">Pregunta por voz: </span>
          </>
        )}
        {text}
      </p>
    </div>
  )
}

function AnswerBubble({
  exchange,
  speech,
  onShowSection,
}: {
  exchange: TutorExchange
  speech: MessageListProps['speech']
  onShowSection: (key: string) => void
}) {
  const insufficient = exchange.status === 'insufficient_information'
  const speaking = speech.speakingId === exchange.id

  return (
    <div
      className={cn(
        'mr-8 space-y-2 rounded-2xl rounded-bl-sm border px-3 py-2 text-sm',
        insufficient ? 'border-dashed bg-muted/50' : 'bg-card',
      )}
    >
      {insufficient && (
        <p className="flex items-center gap-1 text-xs font-medium text-muted-foreground">
          <Info className="size-3.5" aria-hidden="true" />
          La lección no cubre esta pregunta
        </p>
      )}

      {/* Rendered as text: React escapes it, so model output can never inject markup. */}
      <p className="leading-relaxed whitespace-pre-wrap">{exchange.answer}</p>

      <div className="flex flex-wrap items-center gap-1.5">
        {exchange.citations.length > 0 && (
          <span className="text-xs text-muted-foreground">Fuentes:</span>
        )}
        {exchange.citations.map((citation) => (
          <Button
            key={citation.key}
            type="button"
            variant="secondary"
            size="xs"
            onClick={() => onShowSection(citation.key)}
            aria-label={`Ver la sección «${citation.title}» en la lección`}
          >
            {citation.title}
          </Button>
        ))}

        {speech.availability !== 'unsupported' && (
          <Button
            type="button"
            variant="ghost"
            size="xs"
            className="ml-auto"
            disabled={speech.availability !== 'ready'}
            title={
              speech.availability === 'no_spanish_voice'
                ? 'Tu navegador no tiene una voz en español'
                : undefined
            }
            onClick={() => (speaking ? speech.stop() : speech.speak(exchange.id, exchange.answer))}
          >
            {speaking ? <Square aria-hidden="true" /> : <Volume2 aria-hidden="true" />}
            {speaking ? 'Detener' : 'Escuchar'}
          </Button>
        )}
      </div>
    </div>
  )
}
