import { Navigate, Outlet, useLocation } from 'react-router'
import { Alert, AlertDescription } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
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

  // A network or server failure is not "logged out": offer a retry instead of the login screen.
  if (session.isError) {
    return (
      <div className="mx-auto max-w-md space-y-3 p-6">
        <Alert variant="destructive">
          <AlertDescription>
            No hay conexión con el servidor. Revisa tu red e inténtalo de nuevo.
          </AlertDescription>
        </Alert>
        <Button onClick={() => void session.refetch()}>Reintentar</Button>
      </div>
    )
  }

  if (!session.data) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}
