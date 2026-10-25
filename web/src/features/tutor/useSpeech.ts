import { useCallback, useEffect, useMemo, useState } from 'react'
import { rankSpanishVoices, splitIntoSpeechChunks, voiceQuality } from '@/shared/media/speech'
import { prepareTextForSpeech } from '@/shared/media/speechText'

export type SpeechAvailability = 'unsupported' | 'loading' | 'no_spanish_voice' | 'ready'

const VOICE_LOAD_TIMEOUT_MS = 1_500
const VOICE_STORAGE_KEY = 'tutor.voice'
// Slightly slower than the default reads more naturally for explanations.
const SPEECH_RATE = 0.95

function isSpeechSupported(): boolean {
  return (
    typeof window !== 'undefined' &&
    'speechSynthesis' in window &&
    typeof SpeechSynthesisUtterance === 'function'
  )
}

function readStoredVoice(): string | null {
  try {
    return window.localStorage.getItem(VOICE_STORAGE_KEY)
  } catch {
    return null
  }
}

function storeVoice(voiceUri: string) {
  try {
    window.localStorage.setItem(VOICE_STORAGE_KEY, voiceUri)
  } catch {
    // Storage may be blocked (private mode); the choice then lasts for this visit only.
  }
}

/**
 * Reads tutor answers aloud with the browser's speech synthesis, using the most natural Spanish voice
 * available (or the one the student picked). The text is adapted for speech and read sentence by
 * sentence; playback only starts from a click and stops when another answer starts or the page goes away.
 */
export function useSpeech() {
  const supported = isSpeechSupported()
  const [voices, setVoices] = useState<SpeechSynthesisVoice[]>([])
  const [voicesSettled, setVoicesSettled] = useState(false)
  const [selectedUri, setSelectedUri] = useState<string | null>(() =>
    supported ? readStoredVoice() : null,
  )
  const [speakingId, setSpeakingId] = useState<string | null>(null)

  useEffect(() => {
    if (!supported) return
    const synth = window.speechSynthesis

    const updateVoices = () => {
      const available = synth.getVoices()
      setVoices(rankSpanishVoices(available))
      if (available.length > 0) setVoicesSettled(true)
    }

    updateVoices()
    synth.addEventListener('voiceschanged', updateVoices)
    // Some browsers never fire voiceschanged when they have no voices at all.
    const timer = window.setTimeout(() => setVoicesSettled(true), VOICE_LOAD_TIMEOUT_MS)

    return () => {
      synth.removeEventListener('voiceschanged', updateVoices)
      window.clearTimeout(timer)
      synth.cancel()
    }
  }, [supported])

  const voice = useMemo(
    () => voices.find((candidate) => candidate.voiceURI === selectedUri) ?? voices[0],
    [voices, selectedUri],
  )

  const selectVoice = useCallback((voiceUri: string) => {
    setSelectedUri(voiceUri)
    storeVoice(voiceUri)
  }, [])

  const stop = useCallback(() => {
    if (supported) window.speechSynthesis.cancel()
    setSpeakingId(null)
  }, [supported])

  const speak = useCallback(
    (id: string, text: string) => {
      if (!supported) return
      const synth = window.speechSynthesis
      synth.cancel()

      const chunks = splitIntoSpeechChunks(prepareTextForSpeech(text))
      // Everything is queued synchronously inside the click handler: iOS requires a user gesture.
      chunks.forEach((chunk, index) => {
        const utterance = new SpeechSynthesisUtterance(chunk)
        utterance.lang = voice?.lang ?? 'es-ES'
        if (voice) utterance.voice = voice
        utterance.rate = SPEECH_RATE
        if (index === chunks.length - 1) {
          utterance.onend = () => setSpeakingId((current) => (current === id ? null : current))
        }
        utterance.onerror = (event) => {
          if (event.error === 'interrupted' || event.error === 'canceled') return
          setSpeakingId(null)
        }
        synth.speak(utterance)
      })

      setSpeakingId(id)
    },
    [supported, voice],
  )

  const availability: SpeechAvailability = !supported
    ? 'unsupported'
    : voice
      ? 'ready'
      : voicesSettled
        ? 'no_spanish_voice'
        : 'loading'

  return {
    availability,
    voices,
    voice,
    hasNaturalVoice: voices.some((candidate) => voiceQuality(candidate) === 'natural'),
    selectVoice,
    speakingId,
    speak,
    stop,
  }
}
