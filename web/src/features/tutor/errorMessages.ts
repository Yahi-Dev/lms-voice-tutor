import { ApiError } from '@/shared/api/client'
import { MicrophoneError } from '@/shared/media/recording'
import type { TutorError } from './tutorReducer'

const MICROPHONE_MESSAGES: Record<MicrophoneError['kind'], string> = {
  denied:
    'El permiso del micrófono está bloqueado. Actívalo desde el icono junto a la dirección del sitio, o escribe tu pregunta.',
  not_found: 'No se encontró un micrófono. Conecta uno o escribe tu pregunta.',
  busy: 'El micrófono está ocupado o falló. Cierra otras aplicaciones que lo usen e inténtalo de nuevo.',
  unsupported:
    'Este navegador no permite grabar en esta página. Abre la aplicación en http://localhost con Chrome, Edge o Firefox, o escribe tu pregunta.',
  too_short: 'La grabación fue demasiado corta. Graba al menos un segundo.',
  unknown: 'No se pudo usar el micrófono. Puedes escribir tu pregunta.',
}

export function microphoneError(error: MicrophoneError): TutorError {
  return {
    kind: `microphone_${error.kind}`,
    message: MICROPHONE_MESSAGES[error.kind],
    retryable: false,
  }
}

/** Spanish, actionable copy for every failure the tutor flow can hit. */
export function requestError(error: unknown, step: 'send' | 'transcribe'): TutorError {
  if (!(error instanceof ApiError)) {
    return { kind: 'unknown', message: 'Algo salió mal. Inténtalo de nuevo.', retryable: true }
  }

  const base = {
    kind: error.code ?? error.kind,
    retryable: error.retryable,
    retryAfterSeconds: error.retryAfterSeconds,
  }
  const service = step === 'send' ? 'El tutor' : 'El servicio de transcripción'

  switch (error.kind) {
    case 'network':
      return {
        ...base,
        message: 'Sin conexión. Tu pregunta se conservó; reintenta cuando vuelva la red.',
      }
    case 'timeout':
      return { ...base, message: `${service} tardó demasiado en responder.` }
    case 'rate_limited':
      return {
        ...base,
        message: error.retryAfterSeconds
          ? `Hay demasiadas solicitudes en este momento. Espera ${error.retryAfterSeconds} segundos y vuelve a intentarlo.`
          : 'Hay demasiadas solicitudes en este momento. Espera un momento y vuelve a intentarlo.',
      }
    case 'unavailable':
      return {
        ...base,
        message:
          step === 'send'
            ? 'El tutor no está disponible en este momento. Tu pregunta se conservó.'
            : 'El servicio de transcripción no está disponible. Puedes escribir tu pregunta.',
      }
    case 'unauthorized':
      return { ...base, message: 'Tu sesión expiró. Vuelve a iniciar sesión.' }
    case 'forbidden':
      return {
        ...base,
        message:
          error.code === 'csrf_validation_failed'
            ? 'La solicitud fue rechazada por seguridad. Recarga la página e inténtalo de nuevo.'
            : 'No tienes acceso a esta lección.',
      }
    case 'validation':
      return {
        ...base,
        message: error.problem?.errors?.question?.[0] ?? 'Revisa tu pregunta e inténtalo de nuevo.',
      }
    case 'too_large':
      return { ...base, message: 'La grabación es demasiado larga. Graba una pregunta más corta.' }
    case 'unsupported':
      return { ...base, message: 'No se pudo procesar el formato del audio. Escribe tu pregunta.' }
    case 'unprocessable':
      return {
        ...base,
        message:
          error.code === 'no_speech_detected'
            ? 'No se detectó voz en la grabación. Habla más cerca del micrófono e inténtalo de nuevo.'
            : 'No se pudo procesar el audio. Inténtalo de nuevo o escribe tu pregunta.',
      }
    default:
      return { ...base, message: 'Algo salió mal. Inténtalo de nuevo.' }
  }
}
