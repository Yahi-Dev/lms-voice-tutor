import { Navigate, Outlet, useLocation } from 'react-router'
import { Skeleton } from '@/shared/ui/skeleton'
import { useSession } from './useSession'

/** Client-side guard for a better UX only: the API enforces access on every request. */
export function RequireAuth() {
  const location = useLocation()
  const session = useSession()

  if (session.isPending) {
    return (
      <div className="mx-auto max-w-5xl space-y-3 p-6" aria-busy="true">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-32 w-full" />
      </div>
    )
  }

  if (!session.data) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}
