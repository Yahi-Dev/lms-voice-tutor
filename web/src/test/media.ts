/**
 * Minimal stand-ins for browser media APIs that jsdom does not implement.
 * Globals are restored automatically after each test (unstubGlobals).
 */

class FakeMediaRecorder {
  static isTypeSupported = (type: string) => type.startsWith('audio/webm')

  state: RecordingState = 'inactive'
  readonly mimeType: string
  ondataavailable: ((event: { data: Blob }) => void) | null = null
  onstop: (() => void) | null = null
  onerror: ((event: Event) => void) | null = null

  constructor(_stream: MediaStream, options?: MediaRecorderOptions) {
    this.mimeType = options?.mimeType ?? 'audio/webm'
  }

  start() {
    this.state = 'recording'
  }

  stop() {
    this.state = 'inactive'
    this.ondataavailable?.({ data: new Blob(['fake-audio'], { type: this.mimeType }) })
    this.onstop?.()
  }
}

export function installMicrophone(getUserMedia: () => Promise<unknown>) {
  Object.defineProperty(window, 'isSecureContext', { configurable: true, value: true })
  vi.stubGlobal('MediaRecorder', FakeMediaRecorder)
  Object.defineProperty(navigator, 'mediaDevices', {
    configurable: true,
    value: { getUserMedia: vi.fn(getUserMedia) },
  })
}

export function workingMicrophone() {
  const track = { stop: vi.fn(), addEventListener: vi.fn() }
  const stream = { getTracks: () => [track], getAudioTracks: () => [track] }
  installMicrophone(() => Promise.resolve(stream))
  return track
}

export function deniedMicrophone() {
  installMicrophone(() => Promise.reject(new DOMException('Permission denied', 'NotAllowedError')))
}

export function installSpeechSynthesis() {
  const spoken: string[] = []

  vi.stubGlobal(
    'SpeechSynthesisUtterance',
    class {
      text: string
      lang = ''
      voice: unknown = null
      onend: (() => void) | null = null
      onerror: (() => void) | null = null
      constructor(text: string) {
        this.text = text
      }
    },
  )

  vi.stubGlobal('speechSynthesis', {
    getVoices: () => [{ lang: 'es-CO', name: 'Voz de prueba', localService: true }],
    speak: vi.fn((utterance: { text: string }) => spoken.push(utterance.text)),
    cancel: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
  })

  return spoken
}
