/** Formats a duration as m:ss, e.g. 65_000 -> "1:05". */
export function formatElapsed(ms: number): string {
  const totalSeconds = Math.floor(ms / 1000)
  return `${Math.floor(totalSeconds / 60)}:${String(totalSeconds % 60).padStart(2, '0')}`
}
