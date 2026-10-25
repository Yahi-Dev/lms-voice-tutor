import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { Alert, AlertDescription } from '@/shared/ui/alert'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card'
import { Skeleton } from '@/shared/ui/skeleton'
import { coursesQueryKey, fetchCourses } from './api'

export function CoursesPage() {
  const courses = useQuery({
    queryKey: coursesQueryKey,
    queryFn: ({ signal }) => fetchCourses(signal),
  })

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Mis cursos</h1>

      {courses.isPending && <Skeleton className="h-40 w-full" />}

      {courses.isError && (
        <Alert variant="destructive">
          <AlertDescription>
            No se pudieron cargar tus cursos. Revisa tu conexión y recarga la página.
          </AlertDescription>
        </Alert>
      )}

      {courses.data?.length === 0 && (
        <Alert>
          <AlertDescription>Aún no tienes cursos asignados.</AlertDescription>
        </Alert>
      )}

      {courses.data?.map((course) => (
        <Card key={course.id}>
          <CardHeader>
            <CardTitle>
              <h2 className="text-lg font-semibold">{course.title}</h2>
            </CardTitle>
            <CardDescription>{course.description}</CardDescription>
          </CardHeader>
          <CardContent>
            <ol className="space-y-2">
              {course.lessons.map((lesson) => (
                <li key={lesson.id}>
                  <Link
                    to={`/lessons/${lesson.id}`}
                    className="block rounded-lg border p-3 transition-colors hover:bg-muted focus-visible:outline-2 focus-visible:outline-ring"
                  >
                    <span className="font-medium">
                      {lesson.position}. {lesson.title}
                    </span>
                    <span className="block text-sm text-muted-foreground">{lesson.summary}</span>
                  </Link>
                </li>
              ))}
            </ol>
          </CardContent>
        </Card>
      ))}
    </div>
  )
}
