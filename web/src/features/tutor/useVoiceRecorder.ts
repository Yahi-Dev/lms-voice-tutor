import { useCallback, useEffect, useRef, useState } from 'react'
import {
  isRecordingSupported,
  MicrophoneError,
  pickAudioMimeType,
  toMicrophoneError,
} from '@/shared/media/recording'

interface VoiceRecorderOptions {
  maxDurationMs?: number
  minDurationMs?: number
  onStarted: (startedAt: number) => void
  onRecorded: (audio: Blob) => void
  onError: (error: MicrophoneError) => void
}

const AUDIO_CONSTRAINTS: MediaStreamConstraints = {
  audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true, channelCount: 1 },
}

/**
 * Records one question with MediaRecorder. The microphone is requested only from a user action and
 * released as soon as recording stops, is cancelled or the component goes away.
 */
export function useVoiceRecorder({
  maxDurationMs = 60_000,
  minDurationMs = 1_000,
  onStarted,
  onRecorded,
  onError,
}: VoiceRecorderOptions) {
  const [isRecording, setIsRecording] = useState(false)
  const [elapsedMs, setElapsedMs] = useState(0)

  const recorderRef = useRef<MediaRecorder | null>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const timerRef = useRef<number | undefined>(undefined)
  const cancelledRef = useRef(false)
  const callbacksRef = useRef({ onStarted, onRecorded, onError })

  useEffect(() => {
    callbacksRef.current = { onStarted, onRecorded, onError }
  }, [onStarted, onRecorded, onError])

  const releaseMicrophone = useCallback(() => {
    window.clearInterval(timerRef.current)
    streamRef.current?.getTracks().forEach((track) => track.stop())
    streamRef.current = null
    recorderRef.current = null
    setIsRecording(false)
  }, [])

  const stop = useCallback(() => {
    const recorder = recorderRef.current
    if (recorder && recorder.state !== 'inactive') recorder.stop()
  }, [])

  const cancel = useCallback(() => {
    cancelledRef.current = true
    stop()
  }, [stop])

  const start = useCallback(async () => {
    if (recorderRef.current) return
    if (!isRecordingSupported()) {
      callbacksRef.current.onError(new MicrophoneError('unsupported'))
      return
    }

    let stream: MediaStream
    try {
      stream = await navigator.mediaDevices.getUserMedia(AUDIO_CONSTRAINTS)
    } catch (error) {
      if (error instanceof DOMException && error.name === 'OverconstrainedError') {
        try {
          stream = await navigator.mediaDevices.getUserMedia({ audio: true })
        } catch (fallbackError) {
          callbacksRef.current.onError(toMicrophoneError(fallbackError))
          return
        }
      } else {
        callbacksRef.current.onError(toMicrophoneError(error))
        return
      }
    }

    const mimeType = pickAudioMimeType()
    let recorder: MediaRecorder
    try {
      recorder = new MediaRecorder(stream, mimeType ? { mimeType } : undefined)
    } catch (error) {
      stream.getTracks().forEach((track) => track.stop())
      callbacksRef.current.onError(toMicrophoneError(error))
      return
    }

    const chunks: Blob[] = []
    const startedAt = Date.now()
    cancelledRef.current = false
    streamRef.current = stream
    recorderRef.current = recorder

    recorder.ondataavailable = (event) => {
      if (event.data.size > 0) chunks.push(event.data)
    }

    recorder.onstop = () => {
      const duration = Date.now() - startedAt
      const type = recorder.mimeType || mimeType || 'audio/webm'
      releaseMicrophone()

      if (cancelledRef.current) return
      if (duration < minDurationMs) {
        callbacksRef.current.onError(new MicrophoneError('too_short'))
        return
      }
      callbacksRef.current.onRecorded(new Blob(chunks, { type }))
    }

    recorder.onerror = (event) => {
      cancelledRef.current = true
      releaseMicrophone()
      callbacksRef.current.onError(toMicrophoneError((event as Event & { error?: unknown }).error))
    }

    // The device was unplugged or the permission revoked mid-recording.
    stream.getAudioTracks().forEach((track) => track.addEventListener('ended', () => stop()))

    recorder.start()
    setElapsedMs(0)
    setIsRecording(true)
    callbacksRef.current.onStarted(startedAt)

    timerRef.current = window.setInterval(() => {
      const elapsed = Date.now() - startedAt
      setElapsedMs(elapsed)
      if (elapsed >= maxDurationMs) stop()
    }, 250)
  }, [maxDurationMs, minDurationMs, releaseMicrophone, stop])

  useEffect(() => {
    window.addEventListener('pagehide', cancel)
    return () => {
      window.removeEventListener('pagehide', cancel)
      cancel()
      releaseMicrophone()
    }
  }, [cancel, releaseMicrophone])

  return { isRecording, elapsedMs, maxDurationMs, start, stop, cancel }
}
