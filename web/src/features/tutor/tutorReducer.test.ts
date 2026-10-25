import {
  initialTutorState,
  type PendingQuestion,
  type TutorError,
  tutorReducer,
  type TutorState,
} from './tutorReducer'

const error: TutorError = { kind: 'tutor_unavailable', message: 'No disponible', retryable: true }
const pending: PendingQuestion = {
  clientMessageId: 'id-1',
  text: '¿Qué es POST?',
  inputMode: 'text',
}
const audio = new Blob(['audio'], { type: 'audio/webm' })

const withDraft = (draft: string): TutorState => ({ ...initialTutorState, draft })

describe('tutorReducer', () => {
  it('keeps the draft when sending fails, so the student can retry', () => {
    let state = tutorReducer(withDraft('¿Qué es POST?'), { type: 'SUBMITTED', pending })
    state = tutorReducer(state, { type: 'SEND_FAILED', error })

    expect(state.phase).toEqual({ name: 'error', error, failed: 'send', pending })
    expect(state.draft).toBe('¿Qué es POST?')
  })

  it('clears the draft only after an answer arrives', () => {
    const sending = tutorReducer(withDraft('¿Qué es POST?'), { type: 'SUBMITTED', pending })

    expect(tutorReducer(sending, { type: 'ANSWERED' })).toEqual(initialTutorState)
  })

  it('puts a transcript in the draft for review instead of sending it', () => {
    let state = tutorReducer(initialTutorState, { type: 'RECORDING_STARTED', at: 0 })
    state = tutorReducer(state, { type: 'RECORDING_STOPPED', audio })
    state = tutorReducer(state, { type: 'TRANSCRIBED', text: '¿Para qué sirve POST?' })

    expect(state).toEqual({
      phase: { name: 'idle' },
      draft: '¿Para qué sirve POST?',
      inputMode: 'voice',
      transcriptReady: true,
    })
  })

  it('keeps the recording when transcription fails so it can be retried', () => {
    let state = tutorReducer(initialTutorState, { type: 'RECORDING_STARTED', at: 0 })
    state = tutorReducer(state, { type: 'RECORDING_STOPPED', audio })
    state = tutorReducer(state, { type: 'TRANSCRIPTION_FAILED', error })

    expect(state.phase).toMatchObject({ name: 'error', failed: 'transcribe', audio })
    expect(tutorReducer(state, { type: 'RECORDING_STOPPED', audio }).phase.name).toBe(
      'transcribing',
    )
  })

  it('ignores events that do not apply to the current phase', () => {
    const sending = tutorReducer(withDraft('hola'), { type: 'SUBMITTED', pending })

    expect(tutorReducer(sending, { type: 'DRAFT_CHANGED', text: 'otra' })).toBe(sending)
    expect(tutorReducer(sending, { type: 'RECORDING_STARTED', at: 0 })).toBe(sending)
    expect(tutorReducer(initialTutorState, { type: 'ANSWERED' })).toBe(initialTutorState)
  })

  it('reverts to text mode when the draft is cleared', () => {
    const voiceDraft: TutorState = {
      ...initialTutorState,
      draft: 'POST',
      inputMode: 'voice',
      transcriptReady: true,
    }

    expect(tutorReducer(voiceDraft, { type: 'DRAFT_CHANGED', text: '' })).toMatchObject({
      inputMode: 'text',
      transcriptReady: false,
    })
  })
})
