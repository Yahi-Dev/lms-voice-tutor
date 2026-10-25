import { MicrophoneError, pickAudioMimeType, toMicrophoneError } from './recording'
import { pickSpanishVoice, rankSpanishVoices, splitIntoSpeechChunks, voiceQuality } from './speech'

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

describe('rankSpanishVoices', () => {
  const voice = (name: string, lang: string, localService = true) =>
    ({ name, lang, localService, voiceURI: name }) as SpeechSynthesisVoice

  const helena = voice('Microsoft Helena - Spanish (Spain)', 'es-ES')
  const salome = voice('Microsoft Salome Online (Natural) - Spanish (Colombia)', 'es-CO', false)
  const dalia = voice('Microsoft Dalia Online (Natural) - Spanish (Mexico)', 'es-MX', false)
  const google = voice('Google español', 'es-ES', false)
  const english = voice('Microsoft Zira', 'en-US')

  it('puts natural voices first, then network voices, then robotic local ones', () => {
    expect(rankSpanishVoices([helena, google, dalia, english, salome])).toEqual([
      salome,
      dalia,
      google,
      helena,
    ])
  })

  it('classifies voice quality from the voice name', () => {
    expect(voiceQuality(salome)).toBe('natural')
    expect(voiceQuality(google)).toBe('good')
    expect(voiceQuality(helena)).toBe('basic')
  })

  it('picks the best Spanish voice or nothing', () => {
    expect(pickSpanishVoice([helena, salome])).toBe(salome)
    expect(pickSpanishVoice([english])).toBeUndefined()
  })
})
