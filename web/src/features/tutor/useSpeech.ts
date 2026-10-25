import { useCallback, useEffect, useState } from 'react'
import { pickSpanishVoice, splitIntoSpeechChunks } from '@/shared/media/speech'

export type SpeechAvailability = 'unsupported' | 'loading' | 'no_spanish_voice' | 'ready'

const VOICE_LOAD_TIMEOUT_MS = 1_500

function isSpeechSupported(): boolean {
  return (
    typeof window !== 'undefined' &&
    'speechSynthesis' in window &&
    typeof SpeechSynthesisUtterance === 'function'
  )
}

/**
 * Reads tutor answers aloud with the browser's speech synthesis. Text is spoken sentence by sentence,
 * playback only starts from a click and it stops when another answer starts or the page goes away.
 */
export function useSpeech() {
  const supported = isSpeechSupported()
  const [voice, setVoice] = useState<SpeechSynthesisVoice>()
  const [voicesSettled, setVoicesSettled] = useState(false)
  const [speakingId, setSpeakingId] = useState<string | null>(null)

  useEffect(() => {
    if (!supported) return
    const synth = window.speechSynthesis

    const updateVoices = () => {
      const voices = synth.getVoices()
      setVoice(pickSpanishVoice(voices))
      if (voices.length > 0) setVoicesSettled(true)
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

  const stop = useCallback(() => {
    if (supported) window.speechSynthesis.cancel()
    setSpeakingId(null)
  }, [supported])

  const speak = useCallback(
    (id: string, text: string) => {
      if (!supported) return
      const synth = window.speechSynthesis
      synth.cancel()

      const chunks = splitIntoSpeechChunks(text)
      // Everything is queued synchronously inside the click handler: iOS requires a user gesture.
      chunks.forEach((chunk, index) => {
        const utterance = new SpeechSynthesisUtterance(chunk)
        utterance.lang = voice?.lang ?? 'es-ES'
        if (voice) utterance.voice = voice
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

  return { availability, speakingId, speak, stop }
}
