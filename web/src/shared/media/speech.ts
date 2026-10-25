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

/** Prefers a Latin American Spanish voice, then any Spanish voice; local voices before network ones. */
export function pickSpanishVoice(voices: SpeechSynthesisVoice[]): SpeechSynthesisVoice | undefined {
  const spanish = voices.filter((voice) => voice.lang.toLowerCase().startsWith('es'))
  if (spanish.length === 0) return undefined

  const rank = (voice: SpeechSynthesisVoice) => {
    const index = PREFERRED_LANGUAGES.indexOf(voice.lang.toLowerCase().replace('_', '-'))
    return (index === -1 ? PREFERRED_LANGUAGES.length : index) * 2 + (voice.localService ? 0 : 1)
  }

  return [...spanish].sort((a, b) => rank(a) - rank(b))[0]
}
