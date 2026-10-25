export interface CurrentUser {
  id: string
  email: string
  displayName: string
}

export interface LessonSummary {
  id: string
  slug: string
  title: string
  summary: string
  position: number
}

export interface CourseSummary {
  id: string
  slug: string
  title: string
  description: string
  lessons: LessonSummary[]
}

export interface LessonSection {
  key: string
  title: string
  body: string
}

export interface Lesson {
  id: string
  courseId: string
  courseTitle: string
  slug: string
  title: string
  summary: string
  sections: LessonSection[]
}

export type TutorStatus = 'answered' | 'insufficient_information'

export type InputMode = 'text' | 'voice'

export interface TutorCitation {
  key: string
  title: string
}

export interface TutorExchange {
  id: string
  clientMessageId: string
  question: string
  answer: string
  status: TutorStatus
  inputMode: InputMode
  citations: TutorCitation[]
  createdAt: string
}

export interface SystemStatus {
  tutorProvider: string
  speechProvider: string
  demoMode: boolean
}
