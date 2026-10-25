import type { InputMode } from '@/shared/api/types'

export interface PendingQuestion {
  clientMessageId: string
  text: string
  inputMode: InputMode
}

export interface TutorError {
  kind: string
  message: string
  retryable: boolean
  retryAfterSeconds?: number
}

export type FailedStep = 'record' | 'transcribe' | 'send'

export type TutorPhase =
  | { name: 'idle' }
  | { name: 'recording'; startedAt: number }
  | { name: 'transcribing'; audio: Blob }
  | { name: 'sending'; pending: PendingQuestion }
  | {
      name: 'error'
      error: TutorError
      failed: FailedStep
      audio?: Blob
      pending?: PendingQuestion
    }

/**
 * The draft lives outside the phase so it survives every transition: a failed request or a broken
 * microphone never throws away what the student wrote or dictated.
 */
export interface TutorState {
  phase: TutorPhase
  draft: string
  inputMode: InputMode
  transcriptReady: boolean
}

export type TutorEvent =
  | { type: 'RECORDING_STARTED'; at: number }
  | { type: 'RECORDING_FAILED'; error: TutorError }
  | { type: 'RECORDING_STOPPED'; audio: Blob }
  | { type: 'RECORDING_CANCELLED' }
  | { type: 'TRANSCRIBED'; text: string }
  | { type: 'TRANSCRIPTION_FAILED'; error: TutorError }
  | { type: 'DRAFT_CHANGED'; text: string }
  | { type: 'SUBMITTED'; pending: PendingQuestion }
  | { type: 'ANSWERED' }
  | { type: 'SEND_FAILED'; error: TutorError }
  | { type: 'ERROR_DISMISSED' }

export const initialTutorState: TutorState = {
  phase: { name: 'idle' },
  draft: '',
  inputMode: 'text',
  transcriptReady: false,
}

const canStartNewAction = (phase: TutorPhase) => phase.name === 'idle' || phase.name === 'error'

/** Pure transition function; events that do not apply to the current phase are ignored. */
export function tutorReducer(state: TutorState, event: TutorEvent): TutorState {
  const { phase } = state

  switch (event.type) {
    case 'RECORDING_STARTED':
      return canStartNewAction(phase)
        ? { ...state, phase: { name: 'recording', startedAt: event.at } }
        : state

    case 'RECORDING_FAILED':
      return phase.name === 'idle' || phase.name === 'recording' || phase.name === 'error'
        ? { ...state, phase: { name: 'error', error: event.error, failed: 'record' } }
        : state

    case 'RECORDING_STOPPED':
      return phase.name === 'recording' || (phase.name === 'error' && phase.failed === 'transcribe')
        ? { ...state, phase: { name: 'transcribing', audio: event.audio } }
        : state

    case 'RECORDING_CANCELLED':
      return phase.name === 'recording' || phase.name === 'transcribing'
        ? { ...state, phase: { name: 'idle' } }
        : state

    case 'TRANSCRIBED': {
      if (phase.name !== 'transcribing') return state
      const draft = state.draft.trim() ? `${state.draft.trim()} ${event.text}` : event.text
      return { phase: { name: 'idle' }, draft, inputMode: 'voice', transcriptReady: true }
    }

    case 'TRANSCRIPTION_FAILED':
      return phase.name === 'transcribing'
        ? {
            ...state,
            phase: { name: 'error', error: event.error, failed: 'transcribe', audio: phase.audio },
          }
        : state

    case 'DRAFT_CHANGED':
      if (phase.name === 'sending') return state
      return {
        ...state,
        draft: event.text,
        inputMode: event.text.trim() ? state.inputMode : 'text',
        transcriptReady: event.text.trim() ? state.transcriptReady : false,
      }

    case 'SUBMITTED':
      return canStartNewAction(phase)
        ? { ...state, phase: { name: 'sending', pending: event.pending } }
        : state

    case 'ANSWERED':
      return phase.name === 'sending' ? initialTutorState : state

    case 'SEND_FAILED':
      return phase.name === 'sending'
        ? {
            ...state,
            phase: { name: 'error', error: event.error, failed: 'send', pending: phase.pending },
          }
        : state

    case 'ERROR_DISMISSED':
      return phase.name === 'error' ? { ...state, phase: { name: 'idle' } } : state

    default: {
      const unhandled: never = event
      return unhandled
    }
  }
}
