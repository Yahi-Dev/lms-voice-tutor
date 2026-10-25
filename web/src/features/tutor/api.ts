import { request } from '@/shared/api/client'
import type { InputMode, TutorExchange } from '@/shared/api/types'

export const messagesQueryKey = (lessonId: string) => ['lessons', lessonId, 'messages'] as const

const ASK_TIMEOUT_MS = 60_000
const TRANSCRIBE_TIMEOUT_MS = 70_000

export function fetchMessages(lessonId: string, signal?: AbortSignal): Promise<TutorExchange[]> {
  return request<TutorExchange[]>(`/lessons/${encodeURIComponent(lessonId)}/messages`, { signal })
}

export function askTutor(
  lessonId: string,
  question: { text: string; clientMessageId: string; inputMode: InputMode },
  signal?: AbortSignal,
): Promise<TutorExchange> {
  return request<TutorExchange>(`/lessons/${encodeURIComponent(lessonId)}/messages`, {
    method: 'POST',
    json: {
      question: question.text,
      clientMessageId: question.clientMessageId,
      inputMode: question.inputMode,
    },
    signal,
    timeoutMs: ASK_TIMEOUT_MS,
  })
}

export async function transcribeAudio(
  lessonId: string,
  audio: Blob,
  signal?: AbortSignal,
): Promise<string> {
  const result = await request<{ text: string }>(
    `/lessons/${encodeURIComponent(lessonId)}/transcriptions`,
    {
      method: 'POST',
      body: audio,
      contentType: audio.type || 'audio/webm',
      signal,
      timeoutMs: TRANSCRIBE_TIMEOUT_MS,
    },
  )
  return result.text
}
