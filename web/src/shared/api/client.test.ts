import { http, HttpResponse } from 'msw'
import { apiUrl, problem, server } from '@/test/server'
import { ApiError, CSRF_HEADER, request } from './client'

describe('request', () => {
  it('sends the CSRF header on state-changing requests only', async () => {
    const headers: Record<string, string | null> = {}
    server.use(
      http.get(apiUrl('/probe'), ({ request: incoming }) => {
        headers.get = incoming.headers.get(CSRF_HEADER)
        return HttpResponse.json({})
      }),
      http.post(apiUrl('/probe'), ({ request: incoming }) => {
        headers.post = incoming.headers.get(CSRF_HEADER)
        return HttpResponse.json({})
      }),
    )

    await request('/probe')
    await request('/probe', { method: 'POST', json: {} })

    expect(headers.get).toBeNull()
    expect(headers.post).toBe('1')
  })

  it('turns a problem response into a typed error with its code and retry delay', async () => {
    server.use(
      http.post(apiUrl('/probe'), () => problem(429, 'tutor_rate_limited', { 'Retry-After': '7' })),
    )

    const error = await request('/probe', { method: 'POST', json: {} }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({
      kind: 'rate_limited',
      code: 'tutor_rate_limited',
      retryAfterSeconds: 7,
      retryable: true,
    })
  })

  it.each([
    [403, 'forbidden', false],
    [404, 'not_found', false],
    [503, 'unavailable', true],
    [504, 'timeout', true],
  ] as const)('maps HTTP %i to %s', async (status, kind, retryable) => {
    server.use(http.get(apiUrl('/probe'), () => problem(status, 'any')))

    const error = await request('/probe').catch((e: unknown) => e)

    expect(error).toMatchObject({ kind, retryable })
  })

  it('reports a network failure as retryable', async () => {
    server.use(http.get(apiUrl('/probe'), () => HttpResponse.error()))

    const error = await request('/probe').catch((e: unknown) => e)

    expect(error).toMatchObject({ kind: 'network', retryable: true })
  })
})
