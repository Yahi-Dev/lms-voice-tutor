import { request } from '@/shared/api/client'
import type { CourseSummary, Lesson, SystemStatus } from '@/shared/api/types'

export const coursesQueryKey = ['courses'] as const
export const lessonQueryKey = (lessonId: string) => ['lessons', lessonId] as const
export const systemStatusQueryKey = ['system-status'] as const

export function fetchCourses(signal?: AbortSignal): Promise<CourseSummary[]> {
  return request<CourseSummary[]>('/courses', { signal })
}

export function fetchLesson(lessonId: string, signal?: AbortSignal): Promise<Lesson> {
  return request<Lesson>(`/lessons/${encodeURIComponent(lessonId)}`, { signal })
}

export function fetchSystemStatus(signal?: AbortSignal): Promise<SystemStatus> {
  return request<SystemStatus>('/system/status', { signal })
}
