import { MicrophoneError, pickAudioMimeType, toMicrophoneError } from './recording'
import { pickSpanishVoice, splitIntoSpeechChunks } from './speech'

describe('pickAudioMimeType', () => {
  it('prefers WebM with Opus and falls back to MP4 for older Safari', () => {
    expect(pickAudioMimeType(() => true)).toBe('audio/webm;codecs=opus')
    expect(pickAudioMimeType((type) => type === 'audio/mp4')).toBe('audio/mp4')
    expect(pickAudioMimeType(() => false)).toBeUndefined()
  })
})

describe('toMicrophoneError', () => {
  it.each([
    ['NotAllowedError', 'denied'],
    ['SecurityError', 'denied'],
    ['NotFoundError', 'not_found'],
    ['NotReadableError', 'busy'],
    ['SomethingElse', 'unknown'],
  ])('maps %s to %s', (name, kind) => {
    expect(toMicrophoneError(new DOMException('', name)).kind).toBe(kind)
  })

  it('keeps an existing microphone error', () => {
    const original = new MicrophoneError('too_short')
    expect(toMicrophoneError(original)).toBe(original)
  })
})

describe('splitIntoSpeechChunks', () => {
  it('splits at sentence boundaries', () => {
    expect(splitIntoSpeechChunks('POST crea recursos. ¿Es idempotente? No.')).toEqual([
      'POST crea recursos.',
      '¿Es idempotente?',
      'No.',
    ])
  })

  it('breaks very long sentences into chunks below the limit', () => {
    const chunks = splitIntoSpeechChunks('palabra '.repeat(100), 50)

    expect(chunks.length).toBeGreaterThan(1)
    expect(chunks.every((chunk) => chunk.length <= 50)).toBe(true)
  })
})

describe('pickSpanishVoice', () => {
  const voice = (lang: string, localService = true) =>
    ({ lang, localService, name: lang }) as SpeechSynthesisVoice

  it('prefers Latin American Spanish and local voices', () => {
    expect(
      pickSpanishVoice([voice('en-US'), voice('es-ES'), voice('es-CO', false), voice('es-CO')]),
    ).toEqual(voice('es-CO'))
  })

  it('returns nothing when there is no Spanish voice', () => {
    expect(pickSpanishVoice([voice('en-US')])).toBeUndefined()
  })
})
