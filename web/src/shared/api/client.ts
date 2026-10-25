export type ApiErrorKind =
  | 'network'
  | 'timeout'
  | 'aborted'
  | 'validation'
  | 'unauthorized'
  | 'forbidden'
  | 'not_found'
  | 'conflict'
  | 'too_large'
  | 'unsupported'
  | 'unprocessable'
  | 'rate_limited'
  | 'unavailable'
  | 'unknown'

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  code?: string
  retryable?: boolean
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly kind: ApiErrorKind
  readonly status?: number
  readonly code?: string
  readonly retryAfterSeconds?: number
  readonly problem?: ProblemDetails

  constructor(
    kind: ApiErrorKind,
    options: {
      status?: number
      problem?: ProblemDetails
      retryAfterSeconds?: number
      cause?: unknown
    } = {},
  ) {
    super(options.problem?.title ?? kind, { cause: options.cause })
    this.name = 'ApiError'
    this.kind = kind
    this.status = options.status
    this.problem = options.problem
    this.code = options.problem?.code
    this.retryAfterSeconds = options.retryAfterSeconds
  }

  get retryable(): boolean {
    return (
      this.kind === 'network' ||
      this.kind === 'timeout' ||
      this.kind === 'rate_limited' ||
      this.kind === 'unavailable'
    )
  }
}

/** Header the API requires on every state-changing request (CSRF protection). */
export const CSRF_HEADER = 'X-CSRF'

const DEFAULT_TIMEOUT_MS = 10_000

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  json?: unknown
  body?: BodyInit
  contentType?: string
  signal?: AbortSignal
  timeoutMs?: number
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const method = options.method ?? 'GET'
  const headers = new Headers({ Accept: 'application/json' })

  if (method !== 'GET') headers.set(CSRF_HEADER, '1')

  let body = options.body
  if (options.json !== undefined) {
    headers.set('Content-Type', 'application/json')
    body = JSON.stringify(options.json)
  } else if (options.contentType) {
    headers.set('Content-Type', options.contentType)
  }

  const timeout = AbortSignal.timeout(options.timeoutMs ?? DEFAULT_TIMEOUT_MS)
  const signal = options.signal ? AbortSignal.any([options.signal, timeout]) : timeout

  let response: Response
  try {
    response = await fetch(new URL(`/api${path}`, window.location.origin), {
      method,
      headers,
      body,
      credentials: 'same-origin',
      signal,
    })
  } catch (error) {
    throw toTransportError(error, options.signal)
  }

  if (!response.ok) throw await toApiError(response)
  if (response.status === 204) return undefined as T

  if (!response.headers.get('Content-Type')?.includes('json')) {
    throw new ApiError('unknown', { status: response.status })
  }

  return (await response.json()) as T
}

function toTransportError(error: unknown, callerSignal?: AbortSignal): ApiError {
  if (callerSignal?.aborted) return new ApiError('aborted', { cause: error })
  if (error instanceof DOMException && error.name === 'TimeoutError') {
    return new ApiError('timeout', { cause: error })
  }
  if (error instanceof DOMException && error.name === 'AbortError') {
    return new ApiError('aborted', { cause: error })
  }
  return new ApiError('network', { cause: error })
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails | undefined
  try {
    if (response.headers.get('Content-Type')?.includes('json')) {
      problem = (await response.json()) as ProblemDetails
    }
  } catch {
    problem = undefined
  }

  const retryAfter = Number(response.headers.get('Retry-After'))
  return new ApiError(kindFromStatus(response.status), {
    status: response.status,
    problem,
    retryAfterSeconds: Number.isFinite(retryAfter) && retryAfter > 0 ? retryAfter : undefined,
  })
}

function kindFromStatus(status: number): ApiErrorKind {
  switch (status) {
    case 400:
      return 'validation'
    case 401:
      return 'unauthorized'
    case 403:
      return 'forbidden'
    case 404:
      return 'not_found'
    case 409:
      return 'conflict'
    case 413:
      return 'too_large'
    case 415:
      return 'unsupported'
    case 422:
      return 'unprocessable'
    case 429:
      return 'rate_limited'
    case 504:
      return 'timeout'
    case 502:
    case 503:
      return 'unavailable'
    default:
      return 'unknown'
  }
}
