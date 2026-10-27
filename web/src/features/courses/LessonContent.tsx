import { Link } from 'react-router'
import type { Lesson } from '@/shared/api/types'
import { cn } from '@/shared/lib/utils'
import { sectionElementId } from './sections'

interface LessonContentProps {
  lesson: Lesson
  highlightedSectionKey?: string
}

export function LessonContent({ lesson, highlightedSectionKey }: LessonContentProps) {
  return (
    <article className="space-y-6">
      <header className="space-y-1">
        <Link to="/" className="text-sm text-muted-foreground hover:underline">
          ← {lesson.courseTitle}
        </Link>
        <h1 className="text-2xl font-semibold">{lesson.title}</h1>
        <p className="text-muted-foreground">{lesson.summary}</p>
      </header>

      {lesson.sections.map((section) => (
        <section
          key={section.key}
          id={sectionElementId(section.key)}
          aria-labelledby={`${sectionElementId(section.key)}-titulo`}
          className={cn(
            'scroll-mt-24 rounded-lg p-3 transition-colors',
            highlightedSectionKey === section.key &&
              'bg-amber-100/70 ring-2 ring-amber-300 dark:bg-amber-900/30',
          )}
        >
          <h2 id={`${sectionElementId(section.key)}-titulo`} className="mb-2 text-lg font-semibold">
            {section.title}
          </h2>
          <p className="leading-relaxed">{section.body}</p>
        </section>
      ))}
    </article>
  )
}
