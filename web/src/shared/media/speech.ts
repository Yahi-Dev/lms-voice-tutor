const MAX_CHUNK_LENGTH = 200

/**
 * Splits text into short chunks at sentence boundaries. Some browsers stop speaking long utterances
 * after a few seconds, so answers are read one chunk at a time.
 */
export function splitIntoSpeechChunks(text: string, maxLength = MAX_CHUNK_LENGTH): string[] {
  const sentences = text
    .replace(/\s+/g, ' ')
    .trim()
    .split(/(?<=[.!?…])\s+/)
    .filter(Boolean)

  const chunks: string[] = []
  for (const sentence of sentences) {
    if (sentence.length <= maxLength) {
      chunks.push(sentence)
      continue
    }

    let current = ''
    for (const word of sentence.split(' ')) {
      if (current && current.length + word.length + 1 > maxLength) {
        chunks.push(current)
        current = word
      } else {
        current = current ? `${current} ${word}` : word
      }
    }
    if (current) chunks.push(current)
  }

  return chunks
}

const PREFERRED_LANGUAGES = ['es-co', 'es-419', 'es-mx', 'es-us', 'es-es']

/**
 * Neural voices sound close to a person: Edge's "Online (Natural)" voices, Apple's enhanced and premium
 * voices. Google's network voices come next; the classic local system voices sound robotic.
 */
const NATURAL_VOICE = /natural|neural|premium|enhanced|mejorad/i
const GOOGLE_VOICE = /google/i

export type VoiceQuality = 'natural' | 'good' | 'basic'

export function voiceQuality(voice: SpeechSynthesisVoice): VoiceQuality {
  if (NATURAL_VOICE.test(voice.name)) return 'natural'
  if (GOOGLE_VOICE.test(voice.name) || !voice.localService) return 'good'
  return 'basic'
}

const QUALITY_RANK: Record<VoiceQuality, number> = { natural: 0, good: 1, basic: 2 }

/** Spanish voices ordered from most to least natural, Latin American accents first within each tier. */
export function rankSpanishVoices(voices: SpeechSynthesisVoice[]): SpeechSynthesisVoice[] {
  const languageRank = (voice: SpeechSynthesisVoice) => {
    const index = PREFERRED_LANGUAGES.indexOf(voice.lang.toLowerCase().replace('_', '-'))
    return index === -1 ? PREFERRED_LANGUAGES.length : index
  }

  return voices
    .filter((voice) => voice.lang.toLowerCase().startsWith('es'))
    .sort(
      (a, b) =>
        QUALITY_RANK[voiceQuality(a)] - QUALITY_RANK[voiceQuality(b)] ||
        languageRank(a) - languageRank(b) ||
        a.name.localeCompare(b.name),
    )
}
