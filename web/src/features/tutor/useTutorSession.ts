import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useReducer, useRef } from 'react'
import { sessionQueryKey } from '@/features/auth/api'
import { ApiError } from '@/shared/api/client'
import type { TutorExchange } from '@/shared/api/types'
import { newId } from '@/shared/lib/ids'
import { askTutor, fetchMessages, messagesQueryKey, transcribeAudio } from './api'
import { microphoneError, requestError } from './errorMessages'
import { initialTutorState, type PendingQuestion, tutorReducer } from './tutorReducer'
import { useVoiceRecorder } from './useVoiceRecorder'

const MIN_QUESTION_LENGTH = 2

/** Wires the tutor state machine to the API, the recorder and the conversation cache. */
export function useTutorSession(lessonId: string) {
  const queryClient = useQueryClient()
  const [state, dispatch] = useReducer(tutorReducer, initialTutorState)
  const requestRef = useRef<AbortController | null>(null)

  const messages = useQuery({
    queryKey: messagesQueryKey(lessonId),
    queryFn: ({ signal }) => fetchMessages(lessonId, signal),
  })

  // A session that expired mid-question sends the student back to the login screen.
  const expireSessionIfUnauthorized = useCallback(
    (error: unknown) => {
      if (error instanceof ApiError && error.kind === 'unauthorized') {
        queryClient.setQueryData(sessionQueryKey, null)
      }
    },
    [queryClient],
  )

  const beginRequest = () => {
    requestRef.current?.abort()
    const controller = new AbortController()
    requestRef.current = controller
    return controller
  }

  useEffect(() => () => requestRef.current?.abort(), [])

  const transcribe = useCallback(
    async (audio: Blob) => {
      dispatch({ type: 'RECORDING_STOPPED', audio })
      const controller = beginRequest()
      try {
        const text = await transcribeAudio(lessonId, audio, controller.signal)
        dispatch({ type: 'TRANSCRIBED', text })
      } catch (error) {
        if (controller.signal.aborted) return
        expireSessionIfUnauthorized(error)
        dispatch({ type: 'TRANSCRIPTION_FAILED', error: requestError(error, 'transcribe') })
      }
    },
    [expireSessionIfUnauthorized, lessonId],
  )

  const recorder = useVoiceRecorder({
    onStarted: useCallback((at: number) => dispatch({ type: 'RECORDING_STARTED', at }), []),
    onRecorded: transcribe,
    onError: useCallback(
      (error) => dispatch({ type: 'RECORDING_FAILED', error: microphoneError(error) }),
      [],
    ),
  })

  const send = useCallback(
    async (pending: PendingQuestion) => {
      dispatch({ type: 'SUBMITTED', pending })
      const controller = beginRequest()
      try {
        const exchange = await askTutor(lessonId, pending, controller.signal)
        queryClient.setQueryData<TutorExchange[]>(messagesQueryKey(lessonId), (current = []) => [
          ...current.filter((existing) => existing.id !== exchange.id),
          exchange,
        ])
        dispatch({ type: 'ANSWERED' })
      } catch (error) {
        if (controller.signal.aborted) return
        expireSessionIfUnauthorized(error)
        dispatch({ type: 'SEND_FAILED', error: requestError(error, 'send') })
      }
    },
    [expireSessionIfUnauthorized, lessonId, queryClient],
  )

  const submit = useCallback(() => {
    const text = state.draft.trim()
    if (text.length < MIN_QUESTION_LENGTH) return
    // Sending the same text again after a failure reuses its id, so the server never stores it twice
    // (the first attempt may have reached it). An edited question is a new question with a new id.
    const { phase } = state
    const failed =
      phase.name === 'error' && phase.pending?.text === text ? phase.pending : undefined
    void send({
      clientMessageId: failed?.clientMessageId ?? newId(),
      text,
      inputMode: state.inputMode,
    })
  }, [send, state])

  const retry = useCallback(() => {
    const { phase } = state
    if (phase.name !== 'error') return
    // Retrying sends what the box shows now, so edits made after the failure are not lost.
    if (phase.failed === 'send') {
      if (state.draft.trim().length >= MIN_QUESTION_LENGTH) submit()
      else if (phase.pending) void send(phase.pending)
    } else if (phase.failed === 'transcribe' && phase.audio) void transcribe(phase.audio)
    else dispatch({ type: 'ERROR_DISMISSED' })
  }, [send, state, submit, transcribe])

  const setDraft = useCallback((text: string) => dispatch({ type: 'DRAFT_CHANGED', text }), [])
  const dismissError = useCallback(() => dispatch({ type: 'ERROR_DISMISSED' }), [])

  const cancelVoice = useCallback(() => {
    recorder.cancel()
    requestRef.current?.abort()
    dispatch({ type: 'RECORDING_CANCELLED' })
  }, [recorder])

  return {
    state,
    messages,
    recorder,
    setDraft,
    submit,
    retry,
    dismissError,
    cancelVoice,
  }
}
