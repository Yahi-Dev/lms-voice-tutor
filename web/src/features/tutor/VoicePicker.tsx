import { voiceQuality } from '@/shared/media/speech'

interface VoicePickerProps {
  voices: SpeechSynthesisVoice[]
  voice?: SpeechSynthesisVoice
  hasNaturalVoice: boolean
  onSelect: (voiceUri: string) => void
}

/** Lets the student choose the reading voice; the most natural one is selected by default. */
export function VoicePicker({ voices, voice, hasNaturalVoice, onSelect }: VoicePickerProps) {
  if (voices.length === 0) return null

  return (
    <div className="space-y-1 text-xs text-muted-foreground">
      {voices.length > 1 && (
        <label className="flex items-center gap-2">
          <span className="shrink-0">Voz de lectura</span>
          <select
            className="h-7 min-w-0 flex-1 rounded-md border bg-background px-2 text-xs text-foreground focus-visible:outline-2 focus-visible:outline-ring"
            value={voice?.voiceURI}
            onChange={(event) => onSelect(event.target.value)}
          >
            {voices.map((candidate) => (
              <option key={candidate.voiceURI} value={candidate.voiceURI}>
                {candidate.name}
                {voiceQuality(candidate) === 'natural' ? ' · natural' : ''}
              </option>
            ))}
          </select>
        </label>
      )}
      {!hasNaturalVoice && (
        <p>
          Para una voz más natural, abre la aplicación en Microsoft Edge (voces neuronales en
          español).
        </p>
      )}
    </div>
  )
}
