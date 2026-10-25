import { Navigate, Route, Routes } from 'react-router'
import { AppLayout } from '@/app/AppLayout'
import { LoginPage } from '@/features/auth/LoginPage'
import { RequireAuth } from '@/features/auth/RequireAuth'
import { CoursesPage } from '@/features/courses/CoursesPage'
import { LessonPage } from '@/features/courses/LessonPage'

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<CoursesPage />} />
          <Route path="lessons/:lessonId" element={<LessonPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
