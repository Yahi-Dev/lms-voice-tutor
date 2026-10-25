/**
 * Rewrites text so a Spanish voice reads it the way a person would. Only the spoken version changes;
 * the screen keeps the original text.
 *
 * Following Spanish usage, acronyms that can be read as a word are said as a word (API, REST, CRUD) and
 * the rest are spelled out with the Spanish names of the letters (HTTP -> "hache te te pe").
 */
const SPOKEN_ACRONYMS: [RegExp, string][] = [
  [/\bHTTPS\b/g, 'hache te te pe ese'],
  [/\bHTTP\b/g, 'hache te te pe'],
  [/\bURLs\b/g, 'u erre eles'],
  [/\bURL\b/g, 'u erre ele'],
  [/\bAPIs\b/g, 'apis'],
  [/\bAPI\b/g, 'api'],
  [/\bJSON\b/g, 'yéison'],
  [/\bREST\b/g, 'rest'],
  [/\bCRUD\b/g, 'crud'],
  [/\bSQL\b/g, 'ese cu ele'],
  [/\bHTML\b/g, 'hache te eme ele'],
  [/\bCSS\b/g, 'ce ese ese'],
  [/\bXML\b/g, 'equis eme ele'],
  [/\bRFC\b/g, 'erre efe ce'],
]

const ABBREVIATIONS: [RegExp, string][] = [
  [/\bp\. ?ej\./gi, 'por ejemplo'],
  [/\be\. ?g\./gi, 'por ejemplo'],
  [/\betc\./gi, 'etcétera'],
  [/\bvs\.?(?=\s)/gi, 'frente a'],
]

/** Reads a resource path as it would be said aloud: "/productos/42" -> "barra productos barra 42". */
function speakPath(path: string): string {
  return path
    .replace(/\?/g, ' con el parámetro ')
    .replace(/=/g, ' igual a ')
    .replace(/&/g, ' y ')
    .replace(/\//g, ' barra ')
    .trim()
}

export function prepareTextForSpeech(text: string): string {
  let spoken = text
    // Links and formatting marks are not read aloud.
    .replace(/https?:\/\/\S+/g, 'el enlace')
    .replace(/[*_`#>]+/g, '')
    .replace(/(^|\s)[-•]\s+/g, '$1')

  // Paths such as /productos/42 or /productos?categoria=libros.
  spoken = spoken.replace(
    /(^|[\s(])(\/[\p{L}\p{N}_\-./?=&]*)/gu,
    (_, before: string, path: string) => `${before}${speakPath(path)}`,
  )

  for (const [pattern, replacement] of [...ABBREVIATIONS, ...SPOKEN_ACRONYMS]) {
    spoken = spoken.replace(pattern, replacement)
  }

  return (
    spoken
      // A colon or semicolon followed by a space is a pause, not something to pronounce (times like 10:30 stay).
      .replace(/\s*;\s*/g, ', ')
      .replace(/:\s+/g, ', ')
      .replace(/\s+([,.!?…])/g, '$1')
      .replace(/\s{2,}/g, ' ')
      .trim()
  )
}
