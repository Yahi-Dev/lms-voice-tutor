/** Formats in order of preference; the server and the speech provider accept all of them. */
const PREFERRED_AUDIO_TYPES = [
  'audio/webm;codecs=opus',
  'audio/webm',
  'audio/ogg;codecs=opus',
  'audio/mp4',
]

export type MicrophoneErrorKind =
  'denied' | 'not_found' | 'busy' | 'unsupported' | 'too_short' | 'unknown'

export class MicrophoneError extends Error {
  readonly kind: MicrophoneErrorKind

  constructor(kind: MicrophoneErrorKind, cause?: unknown) {
    super(kind, { cause })
    this.name = 'MicrophoneError'
    this.kind = kind
  }
}

/** Recording needs a secure context (https or localhost), getUserMedia and MediaRecorder. */
export function isRecordingSupported(): boolean {
  return (
    typeof window !== 'undefined' &&
    window.isSecureContext &&
    typeof navigator.mediaDevices?.getUserMedia === 'function' &&
    typeof window.MediaRecorder === 'function'
  )
}

export function pickAudioMimeType(
  isTypeSupported: (type: string) => boolean = (type) => MediaRecorder.isTypeSupported(type),
): string | undefined {
  return PREFERRED_AUDIO_TYPES.find((type) => isTypeSupported(type))
}

/** Maps getUserMedia / MediaRecorder failures to the cases the UI explains to the student. */
export function toMicrophoneError(error: unknown): MicrophoneError {
  if (error instanceof MicrophoneError) return error

  const name = error instanceof DOMException || error instanceof Error ? error.name : ''
  switch (name) {
    case 'NotAllowedError':
    case 'SecurityError':
      return new MicrophoneError('denied', error)
    case 'NotFoundError':
    case 'OverconstrainedError':
      return new MicrophoneError('not_found', error)
    case 'NotReadableError':
    case 'AbortError':
      return new MicrophoneError('busy', error)
    case 'TypeError':
      return new MicrophoneError('unsupported', error)
    default:
      return new MicrophoneError('unknown', error)
  }
}
