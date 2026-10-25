import { ApiError, request } from '@/shared/api/client'
import type { CurrentUser } from '@/shared/api/types'

export const sessionQueryKey = ['session'] as const

/** The current user, or null when there is no valid session. */
export async function fetchCurrentUser(signal?: AbortSignal): Promise<CurrentUser | null> {
  try {
    return await request<CurrentUser>('/auth/me', { signal })
  } catch (error) {
    if (error instanceof ApiError && error.kind === 'unauthorized') return null
    throw error
  }
}

export function login(email: string, password: string): Promise<CurrentUser> {
  return request<CurrentUser>('/auth/login', { method: 'POST', json: { email, password } })
}

export function logout(): Promise<void> {
  return request<void>('/auth/logout', { method: 'POST' })
}
